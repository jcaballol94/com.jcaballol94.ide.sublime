using System;
using System.Collections.Generic;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;

namespace jCaballol94.IDE.Sublime
{
    public static class Discovery
    {
        private const string SUBLIME_PATH_KEY = "sublime_path";
        private const string SUBLIME_FOLDER = "Sublime Text";
        private const string SUBLIME_APP = "sublime_text.exe";

        static string GetProgramFiles()
        {
#if UNITY_EDITOR_WIN
            return Environment.GetEnvironmentVariable("ProgramFiles");
#elif UNITY_EDITOR_OSX
            return "/Applications";
#endif
        }

        public static CodeEditor.Installation[] GetSublimeTextInstallations()
		{
            if (TryGetCurrentInstallation(out var installations))
                return installations;

            var allDirectories = Directory.EnumerateDirectories(GetProgramFiles());
            foreach (var directory in allDirectories)
            {
                if (directory.EndsWith(SUBLIME_FOLDER))
                {
                    var potentialPath = Path.Combine(directory, SUBLIME_APP);
                    if (File.Exists(potentialPath))
                    {
                        EditorPrefs.SetString(SUBLIME_PATH_KEY, potentialPath);
                        return new CodeEditor.Installation[] { CreateInstallation(potentialPath) };
                    }
                }
            }

            return Array.Empty<CodeEditor.Installation>();
		}

        private static bool TryGetCurrentInstallation(out CodeEditor.Installation[] installations)
        {
            installations = null;
            if (!EditorPrefs.HasKey(SUBLIME_PATH_KEY))
                return false;

            var previousPath = EditorPrefs.GetString(SUBLIME_PATH_KEY);
            if (File.Exists(previousPath))
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
            return path.EndsWith(Path.Combine(SUBLIME_FOLDER, SUBLIME_APP)) && File.Exists(path);
        }
    }
}