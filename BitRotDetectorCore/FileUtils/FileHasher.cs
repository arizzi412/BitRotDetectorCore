﻿// Utils/FileHasher.cs
using System.Security.Cryptography;
using System.IO;

namespace BitRotDetectorCore.FileUtils;

public static class FileHasher
{
    public static string ComputeFileHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexStringLower(hashBytes);
    }
}