using BitRotDetectorCore.FileUtils;
using Microsoft.EntityFrameworkCore;

namespace BitRotDetectorCore.FileDBRepositoryStuff
{
    public class FileDbRepository
    {
        public readonly FileDbContext dbContext;
        private readonly DbCache dbCache;
        private readonly Metadata dbMetadata;

        public FileDbRepository(VolumeRootPath volumeRootPath)
        {
            dbContext = GetOrCreateDB(volumeRootPath);
            dbCache = DbCache.CreateCache(dbContext);
            dbMetadata = dbContext.Metadata.First();
        }


        public void CreateNewFileRecordAndAddToDB(FileInfo fileInfo, FileIdentityKey fileIdentityKey)
        {
            DBFileRecord fileRecord = new()
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


        public void SetScanStartStatus()
        {
            dbMetadata.LastScanStartTime = DateTime.Now;
            dbMetadata.LastScanCompleted = false;
            dbContext.SaveChanges();
        }

        public void RemoveFiles(IEnumerable<DBFileRecord> fileRecords)
        {
            dbContext.RemoveRange(fileRecords);
            dbContext.SaveChanges();
        }

        public void SetScanCompletedStatus()
        {
            dbMetadata.LastScanCompleted = true;
            dbContext.SaveChanges();
        }

        private static void HideFolder(string path)
        {
            if (Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }
        }

        public DBFileRecord? TryFindFileRecord(FileIdentityKey fileIdentityKey) => dbCache.TryFindRecord(fileIdentityKey);

        public void SaveChanges()
        {
            dbContext.SaveChanges();
        }
    }
}
