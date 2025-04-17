using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BitRotDetectorCore
{
    internal static class DirectoryEquivalence
    {
        public static bool DirectoryStructureAndFileNamesEqual(VolumeRootPath root1, VolumeRootPath root2)
        {
            List<string> root1Paths = [.. Directory.EnumerateFiles(root1, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            })];

            var root2Paths = Directory.EnumerateFiles(root2, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            }).ToList();


            var a = root1Paths.Select(PathWithoutRoot).Except(root2Paths.Select(PathWithoutRoot)).ToList();
            var b = root2Paths.Select(PathWithoutRoot).Except(root1Paths.Select(PathWithoutRoot)).ToList();

            return a.Count == 0 && b.Count == 0;
        }


        public static IEnumerable<(string, string)> GetPairsOfEquivalentFiles(List<string> filePaths, List<string> filePathsF) => filePaths.Join(filePathsF, PathWithoutRoot, PathWithoutRoot, (x, y) => (x, y)).ToList();

        static string PathWithoutRoot(string path)
        {
            var root = Path.GetPathRoot(path);
            var rootLength = root.Length;

            return path[rootLength..];
        }

    }
}

