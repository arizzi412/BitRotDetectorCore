using ReFS_Manager_Core;
using System.Diagnostics;

namespace BitRotDetectorCore
{
    public static class Scanner
    {
        public static void Scan(VolumePath volumeRootPath, bool VerifyFileIntegrity)
        {
            FilePath dbPath = Path.Combine(volumeRootPath.ToString(), @"CheckSumDB.DB");

            FileDbContext dbContext = GetDbContext(volumeRootPath.ToString());

            File.SetAttributes(dbPath, File.GetAttributes(dbPath) | FileAttributes.Hidden);  // hide db

            DbCache dbCache = DbCache.CreateCache(dbContext);

            var allPaths = Directory.EnumerateFiles(volumeRootPath.ToString(), "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            }).Select(FilePath.CreateNormalizedFilePath).ToArray();

            Stopwatch stopwatch = Stopwatch.StartNew();

            var dbMetadata = dbContext.Metadata.First();
            dbMetadata.LastScanStartTime = DateTime.Now;
            dbMetadata.LastScanCompleted = false;
            dbContext.SaveChanges();

            HashSet<ulong> currentNTFSFilesId = [];

            foreach (var path in allPaths)
            {
                ProcessFile(path, dbContext, dbCache, currentNTFSFilesId, VerifyFileIntegrity);

                if (stopwatch.ElapsedMilliseconds > 300 * 1000)
                {
                    dbContext.SaveChanges();
                    stopwatch.Restart();
                }

            }

            dbMetadata.LastScanCompleted = true;
            dbContext.SaveChanges();

            if (dbMetadata.LastScanCompleted == true)
            {

                var filesThatDontExistAnymore = dbContext.FileRecords.Where(fileRecord => currentNTFSFilesId.Contains(fileRecord.NTFSFileID)).ToList();
                dbContext.RemoveRange(filesThatDontExistAnymore);
                dbContext.SaveChanges();
            }

        }

        private static void ProcessFile(FilePath filePath, FileDbContext dbContext, DbCache dbCache, HashSet<ulong> currentFiles, bool verifyFileHashes)
        {
            var fileInfo = new FileInfo(filePath);
            var fileIdentityKey = FileIdentifier.GetFileIdentityKey(filePath);
            bool isNewRecord = false;
            bool differencesFound = false;

            if (!fileInfo.Exists) return;

            FileRecord? fileRecord = dbCache.TryFindRecordByFileIdentity(fileIdentityKey);

            // file record not found in db
            if (fileRecord == null)
            {
                isNewRecord = true;
                CreateNewFileRecordAndAddToDB(fileInfo, fileIdentityKey, dbContext, dbCache);
            }
            // file record found
            else
            {
                differencesFound = UpdateFileRecordIfDifferencesFound(fileInfo, fileRecord);

                if (verifyFileHashes)
                {
                    var isFileCorrupt = VerifyFileIntegrity(fileInfo, fileRecord);
                    if (isFileCorrupt)
                    {
                        fileRecord.FailedIntegrityScan = true;
                    }
                }
            }
            if (!isNewRecord && differencesFound)
            {
                dbContext.FileRecords.Update(fileRecord);
            }

            currentFiles.Add(fileIdentityKey.NTFSFileID);
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
            var pathDiffers = fileRecord.Path != fileInfo.FullName;

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

            if (pathDiffers)
            {
                fileRecord.Path = fileInfo.FullName;
            }

            return lastWriteTimesDiffer || pathDiffers;
        }

        private static FileDbContext GetDbContext(string dbPath)
        {
            var dbContext = new FileDbContext(dbPath);
            dbContext.Database.EnsureCreated();
            return dbContext;
        }

    }
}
