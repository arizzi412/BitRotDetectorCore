using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace BitRotDetectorCore.FileDBRepositoryStuff;

public class DbCache
{
    private ConcurrentDictionary<FileIdentityKey, DBFileRecord> identityKeyToFileRecord;

    public static DbCache CreateCache(FileDbContext fileTrackerContext)
    {
        return new DbCache(MakeDictionaryFromDb(fileTrackerContext));
    }

    private DbCache(ConcurrentDictionary<FileIdentityKey, DBFileRecord> identityKeyToFileRecord)
    {
        this.identityKeyToFileRecord = identityKeyToFileRecord;
    }

    public void AddOrUpdate(FileIdentityKey fileIdentityKey, DBFileRecord fileRecord )
    {
        identityKeyToFileRecord[fileIdentityKey] = fileRecord;
    }

    public DBFileRecord? TryFindRecord(FileIdentityKey fileIdentityKey)
    {
        identityKeyToFileRecord.TryGetValue(fileIdentityKey, out var fileRecord);
        return fileRecord;
    }


    private static ConcurrentDictionary<FileIdentityKey, DBFileRecord> MakeDictionaryFromDb(FileDbContext context)
    {

        var fileRecords = context.FileRecords
            .Where(f => !string.IsNullOrEmpty(f.Hash))
            .ToList();

        var identityKeyToFileRecordKVPs = fileRecords.Select(fileRecord =>
            new KeyValuePair<FileIdentityKey, DBFileRecord>(
                new FileIdentityKey(fileRecord.NTFSFileID, fileRecord.VolumeSerialNumber),
                fileRecord));

        return new ConcurrentDictionary<FileIdentityKey, DBFileRecord>(identityKeyToFileRecordKVPs);
    }
}
