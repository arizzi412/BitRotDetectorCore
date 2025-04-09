using BitRotDetectorCore;
Console.WriteLine("test");
Console.ReadLine();
VolumeRootPath volumeRootPath = null;
try
{
    var currentDirectory = Environment.CurrentDirectory;
    volumeRootPath = VolumeRootPath.CreateVerifiedVolumeRootPath(Path.GetPathRoot(currentDirectory)!);
}
catch (Exception ex)
{

    Console.WriteLine(ex.Message);
}

Console.WriteLine(volumeRootPath);
Console.ReadLine();
var progressReporter = new Progress<ScanProgressInfo>(info =>
{
    Console.WriteLine($"[{info.PercentComplete:F1}%] ({info.FilesProcessed}/{info.TotalFiles}) {info.StatusMessage ?? Path.GetFileName(info.CurrentFilePath)}");
    if (info.IsComplete)
    {
        Console.WriteLine("--- Scan Finished ---");
    }
});

Console.ReadLine();

await Task.Run(() => Scanner.Scan(volumeRootPath, false, progressReporter));