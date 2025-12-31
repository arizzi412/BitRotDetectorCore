using BitRotDetectorCore.FileDBRepositoryStuff;
using BitRotDetectorCore.FileUtils;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BitRotDetectorCore;

public static class Scanner
{
    public static void Scan(VolumeRootPath volumeRootPath, bool VerifyFileIntegrity, IProgress<ScanProgressInfo>? progress = null)
    {
        progress?.Report(new ScanProgressInfo { StatusMessage = "Loading database..." });

        FileDbRepository dbRepository = new FileDbRepository(volumeRootPath);

        progress?.Report(new ScanProgressInfo { StatusMessage = "Enumerating files..." });

        var allPaths = Directory.EnumerateFiles(volumeRootPath.ToString(), "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,

        }).Select(FilePath.CreateNormalizedFilePath).ToArray();

        progress?.Report(new ScanProgressInfo { StatusMessage = "Retrieving File Ids..." });
        var PathToIdentityKey = allPaths.ToDictionary(path => path, path => FileIdentifier.GetFileIdentityKey(path));
        var currentFileIdentityKeys = PathToIdentityKey.Values.Select(x => x.NTFSFileID).ToHashSet();

        int totalFiles = allPaths.Length;
        int filesProcessed = 0;

        Stopwatch stopwatch = Stopwatch.StartNew();

        dbRepository.SetScanStartStatus();


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

                var fileIdentyKey = PathToIdentityKey[path];

                ProcessFile(path, dbRepository, fileIdentyKey, VerifyFileIntegrity);

                if (stopwatch.ElapsedMilliseconds > 300 * 1000)
                {
                    dbRepository.SaveChanges();
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

        dbRepository.SetScanCompletedStatus();
        

        var filesThatDontExistAnymore = dbRepository.dbContext.FileRecords.Where(fileRecord => !currentFileIdentityKeys.Contains(fileRecord.NTFSFileID)).ToList();
        dbRepository.RemoveFiles(filesThatDontExistAnymore);
    }

    private static void ProcessFile(FilePath filePath, FileDbRepository dbRepository, FileIdentityKey fileIdentityKey, bool verifyFileHashes)
    {
        var fileInfo = new FileInfo(filePath);
        var lastWriteTimeChanged = false;
        bool pathDiffers;


        if (!fileInfo.Exists) return;

        var fileRecord = dbRepository.TryFindFileRecord(fileIdentityKey);
        var isInDB = fileRecord is not null;

        if (!isInDB)
        {
            dbRepository.CreateNewFileRecordAndAddToDB(fileInfo, fileIdentityKey);
        }
        else
        {
            lastWriteTimeChanged = fileRecord!.LastWriteTime != fileInfo.LastWriteTimeUtc;

            if (lastWriteTimeChanged) 
                UpdateLastWriteTimeAndHash(fileInfo, fileRecord);

            pathDiffers = fileRecord.Path != new FilePath(fileInfo.FullName);

            if (pathDiffers) 
                fileRecord.Path = fileInfo.FullName;

            if (verifyFileHashes && !lastWriteTimeChanged)
            {
                var isFileCorrupt = VerifyFileIntegrity(fileInfo, fileRecord);
                if (isFileCorrupt)
                {
                    fileRecord.FailedIntegrityScan = true;
                }
            }
        }
    }

    private static void UpdateLastWriteTimeAndHash(FileInfo fileInfo, DBFileRecord fileRecord)
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
    private static bool VerifyFileIntegrity(FileInfo fileInfo, DBFileRecord fileRecord)
    {
        string currentHash = FileHasher.ComputeFileHash(fileInfo.FullName);

        bool metadataMatches = fileRecord.LastWriteTime == fileInfo.LastWriteTimeUtc;
        bool hashMismatches = currentHash != fileRecord.Hash;

        return metadataMatches && hashMismatches;
    }

}