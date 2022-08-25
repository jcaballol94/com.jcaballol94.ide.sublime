using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace jCaballol94.IDE.Sublime
{
    internal class SublimeProjectGenerator : ProjectGeneratorBase
    {
        private string m_projectDirectory;

        public override string SolutionPath => Path.Combine(m_projectDirectory, $"{ProjectName}.sublime-project");
        public string omniSharpSolution;

        public SublimeProjectGenerator(ProjectGeneratorSettings settings, string tempFolder) : base(settings, tempFolder)
        {
            m_projectDirectory = m_tempFolder.NormalizePath();
        }

        public override void Sync()
        {
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Where(p => !m_settings.IsInternalizedPackage(p));
            SyncFileIfNotChanged(SolutionPath, SublimeText(packages));
        }
        public override void SyncIfNeeded(string[] affectedFiles, string[] reimportedFiles)
        {
            Sync();
        }

        private string SublimeText(IEnumerable<UnityEditor.PackageManager.PackageInfo> packages)
        {
            var text = string.Format(GetSublimeText(), GetFolderEntries(packages));
            if (!string.IsNullOrEmpty(omniSharpSolution))
            {
                text += ",\n\t\"solution_file\": \"" + omniSharpSolution.Replace('\\', '/') + "\"";
            }
            text += "\n}";

            return text;
        }

        static string GetSublimeText()
        {
            return string.Join(WINDOWS_NEWLINE, 
                @"{{", 
                @"    ""folders"":", 
                @"    [",
                @"        {{ ""path"": ""../../Assets"", ""file_exclude_patterns"": [""*.meta""] }},",
                @"        {{ ""path"": ""../../Packages"", ""name"": ""Package Manifest"", ""file_exclude_patterns"": [""packages-lock.json""], ""folder_exclude_patterns"": [""*""] }},",
                @"{0}",
                @"    ]").Replace("    ", "\t");
        }

        string GetFolderEntries(IEnumerable<UnityEditor.PackageManager.PackageInfo> packages)
        {
            var folderEntries = packages.Select(i => string.Format(
                "        {{ \"path\": \"{0}\", \"name\": \"{1}\", \"file_exclude_patterns\": [\"*.meta\"] }}",
                i.resolvedPath,
                i.displayName
            ));

            return string.Join("," + WINDOWS_NEWLINE,
                folderEntries).Replace("\\", "/");
        }
    }
}