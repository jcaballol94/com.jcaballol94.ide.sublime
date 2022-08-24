using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Compilation;
using UnityEngine.Profiling;
using UnityEditor;

namespace jCaballol94.IDE.Sublime
{
    internal class SolutionGenerator : ProjectGeneratorBase
    {
        enum ScriptingLanguage
        {
            None,
            CSharp
        }

        private string[] m_ProjectSupportedExtensions = Array.Empty<string>();
        private static readonly Dictionary<string, ScriptingLanguage> s_builtinSupportedExtensions = new Dictionary<string, ScriptingLanguage>
        {
            { "cs", ScriptingLanguage.CSharp },
            { "uxml", ScriptingLanguage.None },
            { "uss", ScriptingLanguage.None },
            { "shader", ScriptingLanguage.None },
            { "compute", ScriptingLanguage.None },
            { "cginc", ScriptingLanguage.None },
            { "hlsl", ScriptingLanguage.None },
            { "glslinc", ScriptingLanguage.None },
            { "template", ScriptingLanguage.None },
            { "raytrace", ScriptingLanguage.None }
        };

        readonly string s_solutionProjectEntryTemplate = string.Join("\r\n", @"Project(""{{{0}}}"") = ""{1}"", ""{2}"", ""{{{3}}}""", @"EndProject").Replace("    ", "\t");
        readonly string s_solutionProjectConfigurationTemplate = string.Join("\r\n", @"        {{{0}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", @"        {{{0}}}.Debug|Any CPU.Build.0 = Debug|Any CPU").Replace("    ", "\t");
        readonly string s_projectFooterTemplate = string.Join("\r\n", @"  </ItemGroup>", @"  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />", @"  <!-- To modify your build process, add your task inside one of the targets below and uncomment it.", @"       Other similar extension points exist, see Microsoft.Common.targets.", @"  <Target Name=""BeforeBuild"">", @"  </Target>", @"  <Target Name=""AfterBuild"">", @"  </Target>", @"  -->", @"</Project>", @"");
        static readonly string[] s_reimportSyncExtensions = { ".dll", ".asmdef" };

        const string TOOLS_VERSION = "4.0";
        const string PRODUCT_VERSION = "10.0.20506";
        const string BASE_DIRECTORY = ".";
        const string TARGET_FRAMEWORK_VERSION = "v4.7.1";

        public static readonly string MSBuildNamespaceUri = "http://schemas.microsoft.com/developer/msbuild/2003";

        private string m_projectDirectory;

        public override string SolutionPath => Path.Combine(m_projectDirectory, $"{ProjectName}.sln");

        public SolutionGenerator(ProjectGeneratorSettings settings, string tempFolder) : base(settings, tempFolder)
        {
            m_projectDirectory = m_tempFolder.NormalizePath();
        }   

        public override void Sync()
        {
            SetupProjectSupportedExtensions();
            GenerateAndWriteSolutionAndProjects();
        }

        private void SetupProjectSupportedExtensions()
        {
            m_ProjectSupportedExtensions = m_settings.ProjectSupportedExtensions;
        }

        public void GenerateAndWriteSolutionAndProjects()
        {
            // Only synchronize assemblies that have associated source files and ones that we actually want in the project.
            // This also filters out DLLs coming from .asmdef files in packages.
            var assemblies = m_settings.GetAssemblies(ShouldFileBePartOfSolution).ToArray();

            var allAssetProjectParts = GenerateAllAssetProjectParts();

            SyncSolution(assemblies);
            var allProjectAssemblies = RelevantAssembliesForMode(assemblies).ToList();
            foreach (Assembly assembly in allProjectAssemblies)
            {
                var responseFileData = ParseResponseFileData(assembly);
                SyncProject(assembly, allAssetProjectParts, responseFileData);
            }
        }

        public string ProjectFile(Assembly assembly)
        {
            var fileBuilder = new StringBuilder(assembly.name);
            fileBuilder.Append(".csproj");
            return Path.Combine(m_projectDirectory, fileBuilder.ToString());
        }

        public override void SyncIfNeeded(string[] affectedFiles, string[] reimportedFiles)
        {
            Profiler.BeginSample("SolutionSynchronizerSync");
            SetupProjectSupportedExtensions();

            if (!HasFilesBeenModified(affectedFiles, reimportedFiles))
            {
                Profiler.EndSample();
                return;
            }

            var assemblies = m_settings.GetAssemblies(ShouldFileBePartOfSolution);
            var allProjectAssemblies = RelevantAssembliesForMode(assemblies).ToList();
            SyncSolution(allProjectAssemblies);

            var allAssetProjectParts = GenerateAllAssetProjectParts();

            var affectedNames = affectedFiles.Select(asset => m_settings.GetAssemblyNameFromScriptPath(asset)).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Split(new[] { ".dll" }, StringSplitOptions.RemoveEmptyEntries)[0]);
            var reimportedNames = reimportedFiles.Select(asset => m_settings.GetAssemblyNameFromScriptPath(asset)).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Split(new[] { ".dll" }, StringSplitOptions.RemoveEmptyEntries)[0]);
            var affectedAndReimported = new HashSet<string>(affectedNames.Concat(reimportedNames));

            foreach (var assembly in allProjectAssemblies)
            {
                if (!affectedAndReimported.Contains(assembly.name))
                    continue;

                SyncProject(assembly, allAssetProjectParts, ParseResponseFileData(assembly));
            }

            Profiler.EndSample();
        }

        private bool ShouldFileBePartOfSolution(string file)
        {
            // Exclude files coming from packages except if they are internalized.
            if (m_settings.IsInternalizedPackagePath(file))
            {
                return false;
            }

            return HasValidExtension(file);
        }

        private Dictionary<string, string> GenerateAllAssetProjectParts()
        {
            Dictionary<string, StringBuilder> stringBuilders = new Dictionary<string, StringBuilder>();

            foreach (var asset in m_settings.GetAllAssetPaths())
            {
                if (m_settings.IsInternalizedPackagePath(asset))
                {
                    continue;
                }

                string extension = Path.GetExtension(asset);
                if (IsSupportedExtension(extension) && ScriptingLanguage.None == ScriptingLanguageFor(extension))
                {
                    // Find assembly the asset belongs to by adding script extension and using compilation pipeline.
                    var assemblyName = m_settings.GetAssemblyNameFromScriptPath(asset);

                    if (string.IsNullOrEmpty(assemblyName))
                    {
                        continue;
                    }

                    assemblyName = Path.GetFileNameWithoutExtension(assemblyName);

                    if (!stringBuilders.TryGetValue(assemblyName, out var projectBuilder))
                    {
                        projectBuilder = new StringBuilder();
                        stringBuilders[assemblyName] = projectBuilder;
                    }

                    projectBuilder.Append("     <None Include=\"").Append(asset.EscapedRelativePathFor(m_projectDirectory)).Append("\" />").Append(WINDOWS_NEWLINE);
                }
            }

            var result = new Dictionary<string, string>();

            foreach (var entry in stringBuilders)
                result[entry.Key] = entry.Value.ToString();

            return result;
        }

        bool HasValidExtension(string file)
        {
            string extension = Path.GetExtension(file);

            // Dll's are not scripts but still need to be included..
            if (extension == ".dll")
                return true;

            if (file.ToLower().EndsWith(".asmdef"))
                return true;

            return IsSupportedExtension(extension);
        }

        private bool IsSupportedExtension(string extension)
        {
            extension = extension.TrimStart('.');
            if (s_builtinSupportedExtensions.ContainsKey(extension))
                return true;
            if (m_ProjectSupportedExtensions.Contains(extension))
                return true;
            return false;
        }

        private static ScriptingLanguage ScriptingLanguageFor(Assembly assembly)
        {
            return ScriptingLanguageFor(GetExtensionOfSourceFiles(assembly.sourceFiles));
        }

        private static string GetExtensionOfSourceFiles(string[] files)
        {
            return files.Length > 0 ? GetExtensionOfSourceFile(files[0]) : "NA";
        }

        private static string GetExtensionOfSourceFile(string file)
        {
            var ext = Path.GetExtension(file).ToLower();
            ext = ext.Substring(1); //strip dot
            return ext;
        }

        private static ScriptingLanguage ScriptingLanguageFor(string extension)
        {
            return s_builtinSupportedExtensions.TryGetValue(extension.TrimStart('.'), out var result)
                ? result
                : ScriptingLanguage.None;
        }

        private void SyncSolution(IEnumerable<Assembly> assemblies)
        {
            SyncSolutionFileIfNotChanged(SolutionPath, SolutionText(assemblies));
        }

        private static string GetSolutionText()
        {
            return string.Join("\r\n", @"", @"Microsoft Visual Studio Solution File, Format Version {0}", @"# Visual Studio {1}", @"{2}", @"Global", @"    GlobalSection(SolutionConfigurationPlatforms) = preSolution", @"        Debug|Any CPU = Debug|Any CPU", @"    EndGlobalSection", @"    GlobalSection(ProjectConfigurationPlatforms) = postSolution", @"{3}", @"    EndGlobalSection", @"    GlobalSection(SolutionProperties) = preSolution", @"        HideSolutionNode = FALSE", @"    EndGlobalSection", @"EndGlobal", @"").Replace("    ", "\t");
        }

        private string SolutionText(IEnumerable<Assembly> assemblies)
        {
            // Same as VSCode
            var fileversion = "11.00";
            var vsversion = "2010";

            var relevantAssemblies = RelevantAssembliesForMode(assemblies);
            string projectEntries = GetProjectEntries(relevantAssemblies);
            string projectConfigurations = string.Join(WINDOWS_NEWLINE, relevantAssemblies.Select(i => GetProjectActiveConfigurations(ProjectGuid(i.name))).ToArray());
            return string.Format(GetSolutionText(), fileversion, vsversion, projectEntries, projectConfigurations);
        }

        private static IEnumerable<Assembly> RelevantAssembliesForMode(IEnumerable<Assembly> assemblies)
        {
            return assemblies.Where(i => ScriptingLanguage.CSharp == ScriptingLanguageFor(i));
        }

        private string GetProjectEntries(IEnumerable<Assembly> assemblies)
        {
            var projectEntries = assemblies.Select(i => string.Format(
                s_solutionProjectEntryTemplate,
                SolutionGuid(i),
                i.name,
                Path.GetFileName(ProjectFile(i)),
                ProjectGuid(i.name)
            ));

            return string.Join(WINDOWS_NEWLINE, projectEntries.ToArray());
        }

        private string SolutionGuid(Assembly assembly)
        {
            return SolutionGuidGenerator.GuidForSolution(ProjectName, GetExtensionOfSourceFiles(assembly.sourceFiles));
        }

        private string ProjectGuid(string assembly)
        {
            return SolutionGuidGenerator.GuidForProject(ProjectName + assembly);
        }

        private string GetProjectActiveConfigurations(string projectGuid)
        {
            return string.Format(
                s_solutionProjectConfigurationTemplate,
                projectGuid);
        }

        private void SyncSolutionFileIfNotChanged(string path, string newContents)
        {
            newContents = OnGeneratedSlnSolution(path, newContents);

            SyncFileIfNotChanged(path, newContents);
        }

        private static string OnGeneratedSlnSolution(string path, string content)
        {
            return InvokeAssetPostProcessorGenerationCallbacks(nameof(OnGeneratedSlnSolution), path, content);
        }

        private bool HasFilesBeenModified(string[] affectedFiles, string[] reimportedFiles)
        {
            return affectedFiles.Any(ShouldFileBePartOfSolution) || reimportedFiles.Any(ShouldSyncOnReimportedAsset);
        }

        private static bool ShouldSyncOnReimportedAsset(string asset)
        {
            return s_reimportSyncExtensions.Contains(new FileInfo(asset).Extension);
        }

        private List<ResponseFileData> ParseResponseFileData(Assembly assembly)
        {
            var systemReferenceDirectories = CompilationPipeline.GetSystemAssemblyDirectories(assembly.compilerOptions.ApiCompatibilityLevel);

            Dictionary<string, ResponseFileData> responseFilesData = assembly.compilerOptions.ResponseFiles.ToDictionary(x => x, x => m_settings.ParseResponseFile(
                x,
                m_projectDirectory,
                systemReferenceDirectories
            ));

            Dictionary<string, ResponseFileData> responseFilesWithErrors = responseFilesData.Where(x => x.Value.Errors.Any())
                .ToDictionary(x => x.Key, x => x.Value);

            if (responseFilesWithErrors.Any())
            {
                foreach (var error in responseFilesWithErrors)
                    foreach (var valueError in error.Value.Errors)
                    {
                        UnityEngine.Debug.LogError($"{error.Key} Parse Error : {valueError}");
                    }
            }

            return responseFilesData.Select(x => x.Value).ToList();
        }

        private void SyncProject(
            Assembly assembly,
            Dictionary<string, string> allAssetsProjectParts,
            List<ResponseFileData> responseFilesData)
        {
            SyncProjectFileIfNotChanged(ProjectFile(assembly), ProjectText(assembly, allAssetsProjectParts, responseFilesData));
        }

        private void SyncProjectFileIfNotChanged(string path, string newContents)
        {
            if (Path.GetExtension(path) == ".csproj")
            {
                newContents = OnGeneratedCSProject(path, newContents);
            }

            SyncFileIfNotChanged(path, newContents);
        }

        private string ProjectText(
            Assembly assembly,
            Dictionary<string, string> allAssetsProjectParts,
            List<ResponseFileData> responseFilesData)
        {
            var projectBuilder = new StringBuilder();
            ProjectHeader(assembly, responseFilesData, projectBuilder);
            var references = new List<string>();

            foreach (string file in assembly.sourceFiles)
            {
                var fullFile = file.EscapedRelativePathFor(m_projectDirectory);
                projectBuilder.Append("     <Compile Include=\"").Append(fullFile).Append("\" />").Append(WINDOWS_NEWLINE);
            }

            // Append additional non-script files that should be included in project generation.
            if (allAssetsProjectParts.TryGetValue(assembly.name, out var additionalAssetsForProject))
                projectBuilder.Append(additionalAssetsForProject);

            var responseRefs = responseFilesData.SelectMany(x => x.FullPathReferences.Select(r => r));
            var internalAssemblyReferences = assembly.assemblyReferences
              .Where(i => !i.sourceFiles.Any(ShouldFileBePartOfSolution)).Select(i => i.outputPath);
            var allReferences =
              assembly.compiledAssemblyReferences
                .Union(responseRefs)
                .Union(references)
                .Union(internalAssemblyReferences);

            foreach (var reference in allReferences)
            {
                string fullReference = Path.IsPathRooted(reference) ? reference : Path.Combine(m_projectDirectory, reference);
                AppendReference(fullReference, projectBuilder);
            }

            if (0 < assembly.assemblyReferences.Length)
            {
                projectBuilder.Append("  </ItemGroup>").Append(WINDOWS_NEWLINE);
                projectBuilder.Append("  <ItemGroup>").Append(WINDOWS_NEWLINE);
                foreach (Assembly reference in assembly.assemblyReferences.Where(i => i.sourceFiles.Any(ShouldFileBePartOfSolution)))
                {
                    projectBuilder.Append("    <ProjectReference Include=\"").Append(reference.name).Append(".csproj").Append("\">").Append(WINDOWS_NEWLINE);
                    projectBuilder.Append("      <Project>{").Append(ProjectGuid(reference.name)).Append("}</Project>").Append(WINDOWS_NEWLINE);
                    projectBuilder.Append("      <Name>").Append(reference.name).Append("</Name>").Append(WINDOWS_NEWLINE);
                    projectBuilder.Append("    </ProjectReference>").Append(WINDOWS_NEWLINE);
                }
            }

            projectBuilder.Append(s_projectFooterTemplate);
            return projectBuilder.ToString();
        }

        private static void AppendReference(string fullReference, StringBuilder projectBuilder)
        {
            var escapedFullPath = SecurityElement.Escape(fullReference);
            escapedFullPath = escapedFullPath.NormalizePath();
            projectBuilder.Append("    <Reference Include=\"").Append(Path.GetFileNameWithoutExtension(escapedFullPath)).Append("\">").Append(WINDOWS_NEWLINE);
            projectBuilder.Append("        <HintPath>").Append(escapedFullPath).Append("</HintPath>").Append(WINDOWS_NEWLINE);
            projectBuilder.Append("    </Reference>").Append(WINDOWS_NEWLINE);
        }

        private void ProjectHeader(
            Assembly assembly,
            List<ResponseFileData> responseFilesData,
            StringBuilder builder
        )
        {
            var otherArguments = GetOtherArgumentsFromResponseFilesData(responseFilesData);
            GetProjectHeaderTemplate(
                builder,
                ProjectGuid(assembly.name),
                assembly.name,
                string.Join(";", new[] { "DEBUG", "TRACE" }.Concat(assembly.defines).Concat(responseFilesData.SelectMany(x => x.Defines)).Distinct().ToArray()),
                GenerateLangVersion(otherArguments["langversion"], assembly),
                assembly.compilerOptions.AllowUnsafeCode | responseFilesData.Any(x => x.Unsafe),
                GenerateAnalyserItemGroup(RetrieveRoslynAnalyzers(assembly, otherArguments)),
                GenerateRoslynAnalyzerRulesetPath(assembly, otherArguments)
            );
        }

        private static string GenerateLangVersion(IEnumerable<string> langVersionList, Assembly assembly)
        {
            var langVersion = langVersionList.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(langVersion))
                return langVersion;
#if UNITY_2020_2_OR_NEWER
            return assembly.compilerOptions.LanguageVersion;
#else
            return k_TargetLanguageVersion;
#endif
        }

        private static string GenerateRoslynAnalyzerRulesetPath(Assembly assembly, ILookup<string, string> otherResponseFilesData)
        {
#if UNITY_2020_2_OR_NEWER
            return GenerateAnalyserRuleSet(otherResponseFilesData["ruleset"].Append(assembly.compilerOptions.RoslynAnalyzerRulesetPath).Where(a => !string.IsNullOrEmpty(a)).Distinct().Select(x => MakeAbsolutePath(x).NormalizePath()).ToArray());
#else
            return GenerateAnalyserRuleSet(otherResponseFilesData["ruleset"].Distinct().Select(x => MakeAbsolutePath(x).NormalizePath()).ToArray());
#endif
        }

        private static string GenerateAnalyserRuleSet(string[] paths)
        {
            return paths.Length == 0
                ? string.Empty
                : $"{Environment.NewLine}{string.Join(Environment.NewLine, paths.Select(a => $"    <CodeAnalysisRuleSet>{a}</CodeAnalysisRuleSet>"))}";
        }

        private static ILookup<string, string> GetOtherArgumentsFromResponseFilesData(List<ResponseFileData> responseFilesData)
        {
            var paths = responseFilesData.SelectMany(x =>
            {
                return x.OtherArguments.Where(a => a.StartsWith("/") || a.StartsWith("-"))
                                       .Select(b =>
                                       {
                                           var index = b.IndexOf(":", StringComparison.Ordinal);
                                           if (index > 0 && b.Length > index)
                                           {
                                               var key = b.Substring(1, index - 1);
                                               return new KeyValuePair<string, string>(key, b.Substring(index + 1));
                                           }

                                           const string warnaserror = "warnaserror";
                                           return b.Substring(1).StartsWith(warnaserror)
                                                   ? new KeyValuePair<string, string>(warnaserror, b.Substring(warnaserror.Length + 1))
                                                   : default;
                                       });
            })
              .Distinct()
              .ToLookup(o => o.Key, pair => pair.Value);
            return paths;
        }

        static void GetProjectHeaderTemplate(
            StringBuilder builder,
            string assemblyGUID,
            string assemblyName,
            string defines,
            string langVersion,
            bool allowUnsafe,
            string analyzerBlock,
            string rulesetBlock
        )
        {
            builder.Append(@"<?xml version=""1.0"" encoding=""utf-8""?>").Append(WINDOWS_NEWLINE);
            builder.Append(@"<Project ToolsVersion=""").Append(TOOLS_VERSION).Append(@""" DefaultTargets=""Build"" xmlns=""").Append(MSBuildNamespaceUri).Append(@""">").Append(WINDOWS_NEWLINE);
            builder.Append(@"  <PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <LangVersion>").Append(langVersion).Append("</LangVersion>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  </PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  <PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <ProductVersion>").Append(PRODUCT_VERSION).Append("</ProductVersion>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <SchemaVersion>2.0</SchemaVersion>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <RootNamespace>").Append(EditorSettings.projectGenerationRootNamespace).Append("</RootNamespace>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <ProjectGuid>{").Append(assemblyGUID).Append("}</ProjectGuid>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <OutputType>Library</OutputType>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <AppDesignerFolder>Properties</AppDesignerFolder>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <AssemblyName>").Append(assemblyName).Append("</AssemblyName>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <TargetFrameworkVersion>").Append(TARGET_FRAMEWORK_VERSION).Append("</TargetFrameworkVersion>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <FileAlignment>512</FileAlignment>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <BaseDirectory>").Append(BASE_DIRECTORY).Append("</BaseDirectory>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  </PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <DebugSymbols>true</DebugSymbols>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <DebugType>full</DebugType>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <Optimize>false</Optimize>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <OutputPath>Temp\bin\Debug\</OutputPath>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <DefineConstants>").Append(defines).Append("</DefineConstants>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <ErrorReport>prompt</ErrorReport>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <WarningLevel>4</WarningLevel>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <NoWarn>0169</NoWarn>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <AllowUnsafeBlocks>").Append(allowUnsafe).Append("</AllowUnsafeBlocks>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  </PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"  <PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <NoConfig>true</NoConfig>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <NoStdLib>true</NoStdLib>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <AddAdditionalExplicitAssemblyReferences>false</AddAdditionalExplicitAssemblyReferences>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <ImplicitlyExpandNETStandardFacades>false</ImplicitlyExpandNETStandardFacades>").Append(WINDOWS_NEWLINE);
            builder.Append(@"    <ImplicitlyExpandDesignTimeFacades>false</ImplicitlyExpandDesignTimeFacades>").Append(WINDOWS_NEWLINE);
            builder.Append(rulesetBlock);
            builder.Append(@"  </PropertyGroup>").Append(WINDOWS_NEWLINE);
            builder.Append(analyzerBlock);
            builder.Append(@"  <ItemGroup>").Append(WINDOWS_NEWLINE);
        }

        static string GenerateAnalyserItemGroup(string[] paths)
        {
            //   <ItemGroup>
            //      <Analyzer Include="..\packages\Comments_analyser.1.0.6626.21356\analyzers\dotnet\cs\Comments_analyser.dll" />
            //      <Analyzer Include="..\packages\UnityEngineAnalyzer.1.0.0.0\analyzers\dotnet\cs\UnityEngineAnalyzer.dll" />
            //  </ItemGroup>
            if (paths.Length == 0)
            {
                return string.Empty;
            }

            var analyserBuilder = new StringBuilder();
            analyserBuilder.Append("  <ItemGroup>").Append(WINDOWS_NEWLINE);
            foreach (var path in paths)
            {
                analyserBuilder.Append($"    <Analyzer Include=\"{path.NormalizePath()}\" />").Append(WINDOWS_NEWLINE);
            }

            analyserBuilder.Append("  </ItemGroup>").Append(WINDOWS_NEWLINE);
            return analyserBuilder.ToString();
        }

        string[] RetrieveRoslynAnalyzers(Assembly assembly, ILookup<string, string> otherArguments)
        {
#if UNITY_2020_2_OR_NEWER
            return otherArguments["analyzer"].Concat(otherArguments["a"])
                .SelectMany(x => x.Split(';'))
        .Concat(assembly.compilerOptions.RoslynAnalyzerDllPaths)
                .Select(MakeAbsolutePath)
                .Distinct()
                .ToArray();
#else
      return otherArguments["analyzer"].Concat(otherArguments["a"])
        .SelectMany(x=>x.Split(';'))
        .Distinct()
        .Select(MakeAbsolutePath)
        .ToArray();
#endif
        }

        private static void OnGeneratedCSProjectFiles()
        {
            foreach (var method in GetPostProcessorCallbacks(nameof(OnGeneratedCSProjectFiles)))
            {
                method.Invoke(null, Array.Empty<object>());
            }
        }

        private static string MakeAbsolutePath(string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        }

        private static string OnGeneratedCSProject(string path, string content)
        {
            return InvokeAssetPostProcessorGenerationCallbacks(nameof(OnGeneratedCSProject), path, content);
        }
    }

    internal static class SolutionGuidGenerator
    {
        static MD5 mD5 = MD5CryptoServiceProvider.Create();

        public static string GuidForProject(string projectName)
        {
            return ComputeGuidHashFor(projectName + "salt");
        }

        public static string GuidForSolution(string projectName, string sourceFileExtension)
        {
            return "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
        }

        static string ComputeGuidHashFor(string input)
        {
            var hash = mD5.ComputeHash(Encoding.Default.GetBytes(input));
            return new Guid(hash).ToString();
        }
    }
}