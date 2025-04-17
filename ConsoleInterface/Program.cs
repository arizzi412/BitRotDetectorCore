using BitRotDetectorCore;

var currentDirectory = Environment.CurrentDirectory;
VolumeRootPath volumeRootPath = VolumeRootPath.CreateVerifiedVolumeRootPath(Path.GetPathRoot(currentDirectory)!);

var progressReporter = new Progress<ScanProgressInfo>(info =>
{
    Console.WriteLine($"[{info.PercentComplete:F1}%] ({info.FilesProcessed}/{info.TotalFiles}) {info.StatusMessage ?? Path.GetFileName(info.CurrentFilePath)}");
    if (info.IsComplete)
    {
        Console.WriteLine("--- Scan Finished ---");
    }
});

await Task.Run(() => Scanner.Scan(volumeRootPath, false, progressReporter));