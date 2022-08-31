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
        private readonly CombinedProjectGenerator m_generator = new CombinedProjectGenerator();

        public CodeEditor.Installation[] Installations => Discovery.GetSublimeTextInstallations();

        public void Initialize(string editorInstallationPath)
        {
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
            EditorGUILayout.LabelField("Include in the generated project:");
            EditorGUI.indentLevel++;
            // Show the settings for the generator
            m_generator.OnGUI();
            // Show a button to sync all the projects
            RegenerateProjectFiles();
            EditorGUI.indentLevel--;

            if (m_generator.OmniSharpSupport)
                EditorGUILayout.HelpBox("OmniSharp support is compatible with the OmniSharp sublime package and with my fork of the LSP-OmniSharp sublime package.\nYou can find the fork at https://github.com/jcaballol94/LSP-OmniSharp", MessageType.Info);
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
            CheckCurrentEditorInstallation();

            if (!string.IsNullOrWhiteSpace(filePath))
            {
                filePath = System.IO.Path.GetFullPath(filePath);
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = CodeEditor.CurrentEditorInstallation,
                    Arguments = string.IsNullOrEmpty(filePath) ?
                    $"--project '{GetOrGenerateSolutionFile()}'" :
                     $"--project '{GetOrGenerateSolutionFile()}' '{filePath}':{line}:{column}",
                }
            };
            process.Start();

            return true;
        }

        private static void CheckCurrentEditorInstallation()
        {
            var editorPath = CodeEditor.CurrentEditorInstallation;
            try
            {
                if (Discovery.IsValidPath(editorPath))
                    return;
            }
            catch (System.IO.IOException)
            {
            }

            UnityEngine.Debug.LogWarning($"Sublime Text executable {editorPath} is not found. Please change your settings in Edit > Preferences > External Tools.");
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