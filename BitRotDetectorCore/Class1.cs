// Define this within your BitRotDetectorCore library
namespace BitRotDetectorCore;

public class ScanProgressInfo
{
    public int FilesProcessed { get; init; }
    public int TotalFiles { get; init; }
    public string CurrentFilePath { get; init; } = string.Empty;
    public string? StatusMessage { get; init; } // Optional: For phases like "Starting...", "Scanning...", "Verifying...", "Saving...", "Deleting old records..."
    public bool IsVerificationPhase { get; init; } // To differentiate hashing vs. cleanup
    public bool IsComplete { get; init; } // To signal completion

    // Calculate percentage easily
    public double PercentComplete => TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100.0 : 0.0;
}