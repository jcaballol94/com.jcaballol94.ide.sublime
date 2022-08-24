using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.Linq;

namespace jCaballol94.IDE.Sublime
{
    [InitializeOnLoad]
    public class SublimeTextEditor : IExternalCodeEditor
    {
        private string m_installationPath;
        private readonly ProjectGeneratorBase m_generator = new CombinedProjectGenerator();

        public CodeEditor.Installation[] Installations => Discovery.GetSublimeTextInstallations();

        public void Initialize(string editorInstallationPath)
        {
            m_installationPath = editorInstallationPath;
        }

        static SublimeTextEditor()
        {
            CodeEditor.Register(new SublimeTextEditor());
        }

        public void OnGUI()
        {
            // Show the package info, like the VS package does
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);

            var style = new GUIStyle
            {
                richText = true,
                margin = new RectOffset(0, 4, 0, 0)
            };

            GUILayout.Label($"<size=10><color=grey>{package.displayName} v{package.version} enabled</color></size>", style);
            GUILayout.EndHorizontal();

            // Show the actual settings
            EditorGUILayout.LabelField("Generate .csproj files for:");
            EditorGUI.indentLevel++;
            // Show the settings for the generator
            m_generator.OnGUI();
            // Show a button to sync all the projects
            RegenerateProjectFiles();
            EditorGUI.indentLevel--;
        }

        private void RegenerateProjectFiles()
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(new GUILayoutOption[] { }));
            rect.width = 252;
            if (GUI.Button(rect, "Regenerate project files"))
            {
                m_generator.Sync();
            }
        }

        public bool OpenProject(string filePath = "", int line = -1, int column = -1)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = m_installationPath,
                    Arguments = string.IsNullOrEmpty(filePath) ?
                    $"--project {GetOrGenerateSolutionFile()}" :
                     $"--project {GetOrGenerateSolutionFile()} {filePath}:{line}:{column}",
                }
            };
            process.Start();

            return true;
        }

        private string GetOrGenerateSolutionFile()
        {
            m_generator.Sync();
            return m_generator.SolutionPath;
        }

        public void SyncAll()
        {
            AssetDatabase.Refresh();
            m_generator.Sync();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            m_generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToArray(), importedFiles);
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            if (!Discovery.IsValidPath(editorPath))
            {
                installation = default;
                return false;
            }
            installation = Discovery.CreateInstallation(editorPath);
            return true;
        }
    }
}