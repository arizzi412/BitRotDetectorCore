using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FileRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int FileRecordId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool FailedIntegrityScan { get; set; } = false;
    public long Size { get; set; }
    public DateTime LastWriteTime { get; set; }
    public FilePath Path { get; set; }
    public uint VolumeSerialNumber { get; set; }
    public ulong NTFSFileID { get; set; }

}
