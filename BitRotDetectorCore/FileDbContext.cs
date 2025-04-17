using Microsoft.EntityFrameworkCore;

namespace BitRotDetectorCore;

public class FileDbContext(string dbName) : DbContext
{
    public DbSet<FileRecord> FileRecords { get; set; } = null!;

    public DbSet<Metadata> Metadata { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options
            .UseSqlite($"Data Source={dbName}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // FileRecord configuration
        modelBuilder.Entity<FileRecord>(entity =>
        {
            entity.HasKey(e => e.FileRecordId);
            entity.Property(e => e.FileRecordId)
                  .ValueGeneratedOnAdd();

            entity.HasIndex(e => e.Hash);

            entity.Property(e => e.Path)
                    .HasConversion(
                            path => path.ToString(), // Path object -> string (for DB)
                            value => new FilePath(value)     // string (from DB) -> Path object
      )
      .IsRequired(); // Ensure the path is not null
        });

        modelBuilder.Entity<Metadata>().HasData(new Metadata
        {
            Id = 1,
            LastScanStartTime = DateTime.UtcNow, // Default value
            LastScanCompleted = false
        });
    }
}
