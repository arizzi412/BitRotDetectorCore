using System.Text.RegularExpressions;

namespace ReFS_Manager_Core;

public sealed partial class VolumePath
{
    /// <summary>
    /// Gets the volume path (e.g., "K:\").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VolumePath"/> class.
    /// Validates the input to ensure it is in the form of a drive letter followed by ":\"
    /// and optionally checks that the drive exists.
    /// </summary>
    /// <param name="volumePath">The volume path.</param>
    /// <exception cref="ArgumentException">Thrown when the volume path is invalid.</exception>
    public VolumePath(string volumePath)
    {
        if (string.IsNullOrWhiteSpace(volumePath))
        {
            throw new ArgumentException("Volume path cannot be null or whitespace.", nameof(volumePath));
        }

        // Validate using a regular expression that matches a drive letter followed by :\
        // Example: "C:\", "K:\"
        if (!MyRegex().IsMatch(volumePath))
        {
            throw new ArgumentException("Volume path must be in the format 'X:\\'.", nameof(volumePath));
        }

        // Optionally, check that the drive exists on the system.
        if (!DriveInfo.GetDrives().Any(d => string.Equals(d.Name, volumePath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Specified volume does not exist on this system.", nameof(volumePath));
        }

        Path = volumePath;
    }

    public override string ToString() => Path;

    [GeneratedRegex(@"^[a-zA-Z]:\\$")]
    private static partial Regex MyRegex();
}
