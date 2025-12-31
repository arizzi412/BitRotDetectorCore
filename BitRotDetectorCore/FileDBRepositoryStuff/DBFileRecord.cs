using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitRotDetectorCore.FileDBRepositoryStuff;

public class DBFileRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int FileRecordId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool FailedIntegrityScan { get; set; } = false;
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }
    public required FilePath Path { get; set; }
    public uint VolumeSerialNumber { get; set; }
    public ulong NTFSFileID { get; set; }

}