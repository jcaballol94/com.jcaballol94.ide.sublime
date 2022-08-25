using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SR = System.Reflection;

namespace jCaballol94.IDE.Sublime
{
    internal abstract class ProjectGeneratorBase
    {
        protected const string WINDOWS_NEWLINE = "\r\n";

        protected ProjectGeneratorSettings m_settings;
        protected string m_tempFolder;
        protected string ProjectName => Directory.GetParent(Application.dataPath).Name;

        public abstract string SolutionPath {get;}

        public ProjectGeneratorBase() 
        : this(new ProjectGeneratorSettings())
        {}

        public ProjectGeneratorBase(ProjectGeneratorSettings settings) : this(settings,
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "com.jcaballol94.ide.sublime"))
        {}

        public ProjectGeneratorBase(ProjectGeneratorSettings settings, string tempFolder)
        {
            if (settings == null) throw new System.ArgumentNullException("settings");
            m_settings = settings;
            m_tempFolder = tempFolder;
        }

        public void OnGUI()
        {
            // Simply show the settings
            m_settings.OnGUI();
        }

        public abstract void Sync();

        public abstract void SyncIfNeeded(string[] affectedFiles, string[] importedFiles);

        protected void SyncFileIfNotChanged(string filename, string newContents)
        {
            try
            {
                if (File.Exists(filename) && newContents == File.ReadAllText(filename))
                {
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (!Directory.Exists(m_tempFolder))
                Directory.CreateDirectory(m_tempFolder);
            File.WriteAllText(filename, newContents);
        }

        protected static string InvokeAssetPostProcessorGenerationCallbacks(string name, string path, string content)
        {
            foreach (var method in GetPostProcessorCallbacks(name))
            {
                var args = new[] { path, content };
                var returnValue = method.Invoke(null, args);
                if (method.ReturnType == typeof(string))
                {
                    // We want to chain content update between invocations
                    content = (string)returnValue;
                }
            }

            return content;
        }

        protected static IEnumerable<SR.MethodInfo> GetPostProcessorCallbacks(string name)
        {
            return TypeCache
                .GetTypesDerivedFrom<AssetPostprocessor>()
                .Select(t => t.GetMethod(name, SR.BindingFlags.Public | SR.BindingFlags.NonPublic | SR.BindingFlags.Static))
                .Where(m => m != null);
        }
    }
}