using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace BitRotDetectorCore;

public class DbCache
{
    private ConcurrentDictionary<FileIdentityKey, FileRecord> IdentityKeyToFileRecord { get; init; }

    public static DbCache CreateCache(FileDbContext fileTrackerContext)
    {
        var ds = new DbCache()
        {
            IdentityKeyToFileRecord = MakeDictionaryFromDb(fileTrackerContext),
        };
        return ds;
    }

    public void AddOrUpdate(FileIdentityKey fileIdentityKey, FileRecord fileRecord )
    {
        IdentityKeyToFileRecord[fileIdentityKey] = fileRecord;
    }

    private DbCache()
    {
        IdentityKeyToFileRecord = new ConcurrentDictionary<FileIdentityKey, FileRecord>();
    }

    public FileRecord? TryFindRecordByFileIdentity(FileIdentityKey fileIdentityKey)
    {
        IdentityKeyToFileRecord.TryGetValue(fileIdentityKey, out var fileRecord);
        return fileRecord;
    }


    private static ConcurrentDictionary<FileIdentityKey, FileRecord> MakeDictionaryFromDb(FileDbContext context)
    {

        var fileRecords = context.FileRecords
            .AsNoTracking()
            .Where(f => !string.IsNullOrEmpty(f.Hash))
            .ToList();

        var identityKeyToFileRecordKVPs = fileRecords.Select(fileRecord =>
            new KeyValuePair<FileIdentityKey, FileRecord>(
                new FileIdentityKey(fileRecord.NTFSFileID, fileRecord.VolumeSerialNumber),
                fileRecord));

        var distinct = identityKeyToFileRecordKVPs.DistinctBy(x => x.Key.NTFSFileID).ToList();

        var except = identityKeyToFileRecordKVPs.Except(distinct).ToList();

        return new ConcurrentDictionary<FileIdentityKey, FileRecord>(identityKeyToFileRecordKVPs);
    }
}
