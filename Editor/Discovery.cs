using System;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;

namespace jCaballol94.IDE.Sublime
{
    internal static class Discovery
    {
        private const string SUBLIME_PATH_KEY = "sublime_path";
        private const string SUBLIME_DEFAULT_FOLDER = "Sublime Text";
        private const string SUBLIME_APP = "sublime_text.exe";

        private static string ProgramFiles =>
#if UNITY_EDITOR_WIN
                    Environment.GetEnvironmentVariable("ProgramFiles");
#elif UNITY_EDITOR_OSX
                    "/Applications";
#endif

        public static CodeEditor.Installation[] GetSublimeTextInstallations()
        {
            // If we have found it previously, try to reuse that path
            if (TryGetCurrentInstallation(out var installations))
                return installations;

            bool found = false;
            var programFiles = ProgramFiles;

            // Try the most common path first
            var potentialPath = Path.Combine(programFiles, SUBLIME_DEFAULT_FOLDER, SUBLIME_APP);
            if (IsValidPath(potentialPath))
            {
                found = true;
            }
            else
            {
                // Try all folders in Program Files (or equivalent)
                var allDirectories = Directory.EnumerateDirectories(programFiles);
                foreach (var directory in allDirectories)
                {
                    potentialPath = Path.Combine(directory, SUBLIME_APP);
                    if (IsValidPath(potentialPath))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                EditorPrefs.SetString(SUBLIME_PATH_KEY, potentialPath);
                return new CodeEditor.Installation[] { CreateInstallation(potentialPath) };
            }

            return Array.Empty<CodeEditor.Installation>();
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
            return (path != null && path.EndsWith(SUBLIME_APP) && File.Exists(path));
        }
    }
}