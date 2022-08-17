using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

namespace jCaballol94.IDE.Sublime
{
    [InitializeOnLoad]
    public class SublimeTextEditor : IExternalCodeEditor
    {
        private string m_installationPath;
        public CodeEditor.Installation[] Installations => Discovery.GetSublimeTextInstallations();

        private readonly ProjectGeneration _generator = new ProjectGeneration();

        static SublimeTextEditor()
        {
            CodeEditor.Register(new SublimeTextEditor());
        }

        public void Initialize(string editorInstallationPath)
        {
            m_installationPath = editorInstallationPath;
        }

        public void OnGUI()
        {
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

            EditorGUILayout.LabelField("Generate .csproj files for:");
            EditorGUI.indentLevel++;
            SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
            SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
            SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
            SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
            SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
            SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
            SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
            SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "Generate the solution with the Player defines instead of the Editor ones");
            RegenerateProjectFiles();
            EditorGUI.indentLevel--;
        }

        void RegenerateProjectFiles()
        {
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(new GUILayoutOption[] { }));
            rect.width = 252;
            if (GUI.Button(rect, "Regenerate project files"))
            {
                _generator.Sync();
            }
        }

        void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
        {
            var prevValue = _generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
            var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
            if (newValue != prevValue)
            {
                _generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
            }
        }

        public bool OpenProject(string filePath = "", int line = -1, int column = -1)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = m_installationPath,
                    Arguments = $"--project {GetOrGenerateSolutionFile()} {filePath}:{line}:{column}",
                }
            };
            process.Start();

            return true;
        }

        private string GetOrGenerateSolutionFile()
        {
            _generator.Sync();
            return _generator.Projectname + ".sublime-project";
        }

        public void SyncAll()
        {
            AssetDatabase.Refresh();
            _generator.Sync();
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
            _generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToArray(), importedFiles);
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