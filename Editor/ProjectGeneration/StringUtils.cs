using System;
using System.IO;
using System.Security;

namespace jCaballol94.IDE.Sublime
{
    internal static class StringUtils
    {
        private const char WinSeparator = '\\';
        private const char UnixSeparator = '/';
        
        public static string NormalizePath(this string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (Path.DirectorySeparatorChar == WinSeparator)
                path = path.Replace(UnixSeparator, WinSeparator);
            if (Path.DirectorySeparatorChar == UnixSeparator)
                path = path.Replace(WinSeparator, UnixSeparator);

            return path.Replace(string.Concat(WinSeparator, WinSeparator), WinSeparator.ToString());
        }

        public static string EscapedRelativePathFor(this string file, string projectDirectory)
        {
            var projectDir = Path.GetFullPath(projectDirectory);

            // We have to normalize the path, because the PackageManagerRemapper assumes
            // dir seperators will be os specific.
            var absolutePath = Path.GetFullPath(file.NormalizePath());
            var path = SkipPathPrefix(absolutePath, projectDir);

            return SecurityElement.Escape(path);
        }

        private static string SkipPathPrefix(string path, string prefix)
        {
            return path.StartsWith($@"{prefix}{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                ? path.Substring(prefix.Length + 1)
                : path;
        }
    }
}