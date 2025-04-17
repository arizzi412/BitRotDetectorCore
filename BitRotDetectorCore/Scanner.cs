using BitRotDetectorCore.FileUtils;
using System.Diagnostics;

namespace BitRotDetectorCore;

public static class Scanner
{
    public static void Scan(VolumeRootPath volumeRootPath, bool VerifyFileIntegrity, IProgress<ScanProgressInfo>? progress = null)
    {
        progress?.Report(new ScanProgressInfo { StatusMessage = "Loading database..." });

        FileDbContext dbContext = GetOrCreateDB(volumeRootPath);
        DbCache dbCache = DbCache.CreateCache(dbContext);

        progress?.Report(new ScanProgressInfo { StatusMessage = "Enumerating files..." });

        var allPaths = Directory.EnumerateFiles(volumeRootPath.ToString(), "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,

        }).Select(FilePath.CreateNormalizedFilePath).ToArray();

        int totalFiles = allPaths.Length;
        int filesProcessed = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        var dbMetadata = dbContext.Metadata.First();
        SetScanStartStatus(dbContext, dbMetadata);

        HashSet<ulong> currentNTFSFilesId = [];

        // Report starting scan
        progress?.Report(new ScanProgressInfo { TotalFiles = totalFiles, FilesProcessed = 0, StatusMessage = "Starting scan..." });

        foreach (var path in allPaths)
        {
            try
            {
                progress?.Report(new ScanProgressInfo
                {
                    FilesProcessed = filesProcessed,
                    TotalFiles = totalFiles,
                    CurrentFilePath = path, // Report the last processed file
                    StatusMessage = $"Scanning: {Path.GetFileName(path.ToString())}"
                });

                ProcessFile(path, dbContext, dbCache, currentNTFSFilesId, VerifyFileIntegrity);

                if (stopwatch.ElapsedMilliseconds > 300 * 1000)
                {
                    dbContext.SaveChanges();
                    stopwatch.Restart();
                }
            }
            catch (Exception ex)
            {

                // Log the error (using your logger or reporting progress)
                progress?.Report(new ScanProgressInfo
                {
                    FilesProcessed = filesProcessed,
                    TotalFiles = totalFiles,
                    CurrentFilePath = path, // Report error for this file
                    StatusMessage = $"Error processing {Path.GetFileName(path.ToString())}: {ex.Message}"
                    // Consider adding an Error property to ScanProgressInfo
                });
            }

            filesProcessed++;

        }

        SetScanCompletedStatus(dbContext, dbMetadata);

        RemoveFilesThatDontExisAnymore(dbContext, currentNTFSFilesId);
    }

    private static void SetScanStartStatus(FileDbContext dbContext, Metadata dbMetadata)
    {
        dbMetadata.LastScanStartTime = DateTime.Now;
        dbMetadata.LastScanCompleted = false;
        dbContext.SaveChanges();
    }

    private static void RemoveFilesThatDontExisAnymore(FileDbContext dbContext, HashSet<ulong> currentNTFSFilesId)
    {
        var filesThatDontExistAnymore = dbContext.FileRecords.Where(fileRecord => !currentNTFSFilesId.Contains(fileRecord.NTFSFileID)).ToList();
        dbContext.RemoveRange(filesThatDontExistAnymore);
        dbContext.SaveChanges();
    }

    private static void SetScanCompletedStatus(FileDbContext dbContext, Metadata dbMetadata)
    {
        dbMetadata.LastScanCompleted = true;
        dbContext.SaveChanges();
    }

    private static void ProcessFile(FilePath filePath, FileDbContext dbContext, DbCache dbCache, HashSet<ulong> currentFiles, bool verifyFileHashes)
    {
        var fileInfo = new FileInfo(filePath);
        var fileIdentityKey = FileIdentifier.GetFileIdentityKey(filePath);
        bool differencesFound = false;
        var lastWriteTimeChanged = false;
        var pathDiffers = false;


        if (!fileInfo.Exists) return;

        var isExistingRecord = dbCache.TryFindRecordByFileIdentity(fileIdentityKey, out FileRecord? fileRecord);

        // file record not found in db
        if (!isExistingRecord)
        {
            CreateNewFileRecordAndAddToDB(fileInfo, fileIdentityKey, dbContext, dbCache);
        }
        // file record found
        else
        {
            lastWriteTimeChanged = fileRecord.LastWriteTime != fileInfo.LastWriteTimeUtc;


            if (lastWriteTimeChanged) 
                UpdateLastWriteTimeAndHash(fileInfo, fileRecord);

            pathDiffers = fileRecord.Path != new FilePath(fileInfo.FullName);

            if (pathDiffers) 
                fileRecord.Path = fileInfo.FullName;


            differencesFound = lastWriteTimeChanged || pathDiffers;
        }

        if (verifyFileHashes && !lastWriteTimeChanged)
        {
            var isFileCorrupt = VerifyFileIntegrity(fileInfo, fileRecord);
            if (isFileCorrupt)
            {
                fileRecord.FailedIntegrityScan = true;
            }
        }

        if (isExistingRecord && differencesFound)
        {
            dbContext.FileRecords.Update(fileRecord);
        }

        currentFiles.Add(fileIdentityKey.NTFSFileID);
    }

    private static void UpdateLastWriteTimeAndHash(FileInfo fileInfo, FileRecord? fileRecord)
    {
        fileRecord.Size = fileInfo.Length;
        string newHash = FileHasher.ComputeFileHash(fileInfo.FullName);
        if (fileRecord.Hash != newHash)
        {
            fileRecord.Hash = newHash;
        }
        fileRecord.LastWriteTime = fileInfo.LastWriteTimeUtc;
    }

    /// <summary>
    /// Returns true if file metadata didn't change but hash did.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="fileInfo"></param>
    /// <param name="fileRecord"></param>
    /// <returns></returns>
    private static bool VerifyFileIntegrity(FileInfo fileInfo, FileRecord fileRecord)
    {
        string currentHash = FileHasher.ComputeFileHash(fileInfo.FullName);

        bool metadataMatches = fileRecord.LastWriteTime == fileInfo.LastWriteTimeUtc;
        bool hashMismatches = currentHash != fileRecord.Hash;

        return metadataMatches && hashMismatches;
    }

    private static void CreateNewFileRecordAndAddToDB(FileInfo fileInfo, FileIdentityKey fileIdentityKey, FileDbContext dbContext, DbCache dbCache)
    {
        FileRecord fileRecord = new()
        {
            Hash = FileHasher.ComputeFileHash(fileInfo.FullName),
            Path = fileInfo.FullName,
            Size = fileInfo.Length,
            FailedIntegrityScan = false,
            LastWriteTime = fileInfo.LastWriteTimeUtc,
            NTFSFileID = fileIdentityKey.NTFSFileID,
            VolumeSerialNumber = fileIdentityKey.VolumeSerialNumber,
        };

        dbContext.FileRecords.Add(fileRecord);
        dbCache.AddOrUpdate(fileIdentityKey, fileRecord);
    }

    /// <summary>
    /// Updates file records if differences were found between recordId and actual file.  True means differences were found.  False means no differences found so doesn't track in EF.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="fileRecord"></param>
    /// <returns></returns>
    private static bool UpdateFileRecordIfDifferencesFound(FileInfo fileInfo, FileRecord fileRecord)
    {
        var lastWriteTimesDiffer = fileRecord.LastWriteTime != fileInfo.LastWriteTimeUtc;

        if (lastWriteTimesDiffer)
        {
            fileRecord.Size = fileInfo.Length;
            string newHash = FileHasher.ComputeFileHash(fileInfo.FullName);
            if (fileRecord.Hash != newHash)
            {
                fileRecord.Hash = newHash;
            }
            fileRecord.LastWriteTime = fileInfo.LastWriteTimeUtc;
        }

        var pathDiffers = fileRecord.Path != new FilePath(fileInfo.FullName);

        if (pathDiffers)
        {
            fileRecord.Path = fileInfo.FullName;
        }

        return lastWriteTimesDiffer || pathDiffers;
    }

    private static FileDbContext GetOrCreateDB(VolumeRootPath volumeRootPath)
    {
        var dbName = "FileIntegrity.db";
        var dbFolderName = ".fileIntegrity";

        var dbFolderPath = Path.Combine(volumeRootPath.ToString(), dbFolderName);

        var dbFilePath = Path.Combine(dbFolderPath, dbName);

        Directory.CreateDirectory(dbFolderPath);
        HideFolder(dbFolderPath);

        var dbContext = new FileDbContext(dbFilePath);
        dbContext.Database.EnsureCreated();

        return dbContext;
    }

    private static void HideFolder(string path)
    {
        if (Directory.Exists(path))
        {
            DirectoryInfo di = Directory.CreateDirectory(path);
            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
        }
    }
}