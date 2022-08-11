using Unity.CodeEditor;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace jCaballol94.IDE.Sublime
{
    [InitializeOnLoad]
    public class SublimeTextEditor : IExternalCodeEditor
    {
        public CodeEditor.Installation[] Installations => Discovery.GetSublimeTextInstallations();

        static SublimeTextEditor()
        {
            CodeEditor.Register(new SublimeTextEditor());
        }

        public void Initialize(string editorInstallationPath)
        {
        }

        public void OnGUI()
        {
            GUILayout.Label("It's alive!");
        }

        public bool OpenProject(string filePath = "", int line = -1, int column = -1)
        {
            return true;
        }

        public void SyncAll()
        {
        }

        public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
        {
        }

        public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
        {
            installation = Discovery.CreateInstallation(editorPath);
            return true;
        }
    }
}