using System;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;

namespace jCaballol94.IDE.Sublime
{
    internal static class Discovery
    {
        private const string SUBLIME_PATH_KEY = "sublime_path";
        private const string SUBLIME_DEFAULT_FOLDER_WINDOWS = "Sublime Text";
        private const string SUBLIME_APP_WINDOWS = "sublime_text.exe";
        private const string SUBLIME_COMMAND = "subl";

        private static string ProgramFiles =>
                    Environment.GetEnvironmentVariable("ProgramFiles");

        public static CodeEditor.Installation[] GetSublimeTextInstallations()
        {
            // If we have found it previously, try to reuse that path
            if (TryGetCurrentInstallation(out var installations))
                return installations;

#if UNITY_EDITOR_WIN
            if (FindInstallationWindows(out var path))
#else
            if (FindInstallationOther(out var path))
#endif
            {
                EditorPrefs.SetString(SUBLIME_PATH_KEY, path);
                return new CodeEditor.Installation[] { CreateInstallation(path) };
            }

            return Array.Empty<CodeEditor.Installation>();
        }

        private static bool FindInstallationWindows(out string path)
        {
            var programFiles = ProgramFiles;

            // Try the most common path first
            var potentialPath = Path.Combine(programFiles, SUBLIME_DEFAULT_FOLDER_WINDOWS, SUBLIME_APP_WINDOWS);
            if (IsValidPath(potentialPath))
            {
                path = potentialPath;
                return true;
            }
            else
            {
                // Try all folders in Program Files (or equivalent)
                var allDirectories = Directory.EnumerateDirectories(programFiles);
                foreach (var directory in allDirectories)
                {
                    potentialPath = Path.Combine(directory, SUBLIME_APP_WINDOWS);
                    if (IsValidPath(potentialPath))
                    {
                        path = potentialPath;
                        return true;
                    }
                }
            }
            path = null;
            return false;
        }

        private static bool FindInstallationOther(out string path)
        {
            if (File.Exists(SUBLIME_COMMAND))
            {
                path = Path.GetFullPath(SUBLIME_COMMAND);
                return true;
            }

            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var pathEntry in values.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(pathEntry, SUBLIME_COMMAND);
                if (File.Exists(fullPath))
                {
                    path = fullPath;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private static bool TryGetCurrentInstallation(out CodeEditor.Installation[] installations)
        {
            installations = null;
            if (!EditorPrefs.HasKey(SUBLIME_PATH_KEY))
                return false;

            var previousPath = EditorPrefs.GetString(SUBLIME_PATH_KEY);
            if (IsValidPath(previousPath))
            {
                installations = new CodeEditor.Installation[] { CreateInstallation(previousPath) };
                return true;
            }

            return false;
        }

        public static CodeEditor.Installation CreateInstallation (string path)
        {
            return new CodeEditor.Installation { Name = "Sublime Text", Path = path };
        }

        public static bool IsValidPath(string path)
        {
#if UNITY_EDITOR_WIN
            return (path != null && path.EndsWith(SUBLIME_APP_WINDOWS) && File.Exists(path));
#else
            return ((path == SUBLIME_COMMAND || path.EndsWith(SUBLIME_COMMAND)) && File.Exists(path));
#endif
        }
    }
}