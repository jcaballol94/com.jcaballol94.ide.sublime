using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor;
using UnityEngine;

namespace jCaballol94.IDE.Sublime
{
    internal class ProjectGeneratorSettings
    {
        private ProjectGenerationFlag m_projectGenerationFlag = (ProjectGenerationFlag)EditorPrefs.GetInt("unity_project_generation_flag", 0);
        private readonly Dictionary<string, UnityEditor.PackageManager.PackageInfo> m_packageInfoCache = new Dictionary<string, UnityEditor.PackageManager.PackageInfo>();

        public string[] ProjectSupportedExtensions => EditorSettings.projectGenerationUserExtensions;
        public bool SupportOmniSharp => m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.OmniSharp);

        public void OnGUI()
        {
            SettingsButton(ProjectGenerationFlag.OmniSharp, "OmniSharp support", "Generate sln and csproj for OmniSharp");
            SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
            SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
            SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
            SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
            SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
            SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
            SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
            EditorGUI.BeginDisabledGroup(!SupportOmniSharp);
            SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "Generate the solution with the Player defines instead of the Editor ones");
            EditorGUI.EndDisabledGroup();
        }

        private void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
        {
            var prevValue = m_projectGenerationFlag.HasFlag(preference);
            var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
            if (newValue != prevValue)
            {
                ToggleProjectGeneration(preference);
            }
        }

        public void ToggleProjectGeneration(ProjectGenerationFlag preference)
        {
            if (m_projectGenerationFlag.HasFlag(preference))
            {
                m_projectGenerationFlag ^= preference;
            }
            else
            {
                m_projectGenerationFlag |= preference;
            }

            // Store the new value
            EditorPrefs.SetInt("unity_project_generation_flag", (int)m_projectGenerationFlag);
        }

        public IEnumerable<Assembly> GetAssemblies(Func<string, bool> shouldFileBePartOfSolution)
        {
            var assemblyType = m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.PlayerAssemblies) ?
                AssembliesType.Player :
                AssembliesType.Editor;
                
            return GetAssembliesByType(assemblyType, shouldFileBePartOfSolution, @"Temp\Bin\Debug\");
        }

        private static IEnumerable<Assembly> GetAssembliesByType(AssembliesType type, Func<string, bool> shouldFileBePartOfSolution, string outputPath)
        {
            foreach (var assembly in CompilationPipeline.GetAssemblies(type))
            {
                if (assembly.sourceFiles.Any(shouldFileBePartOfSolution))
                {
                    yield return new Assembly(
                        assembly.name,
                        outputPath,
                        assembly.sourceFiles,
                        assembly.defines,
                        assembly.assemblyReferences,
                        assembly.compiledAssemblyReferences,
                        assembly.flags,
                        assembly.compilerOptions
#if UNITY_2020_2_OR_NEWER
                        , assembly.rootNamespace
#endif
                    );
                }
            }
        }

        public bool IsInternalizedPackagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }
            var packageInfo = FindForAssetPath(path);
            if (packageInfo == null)
            {
                return false;
            }
            
            return IsInternalizedPackage(packageInfo);
        }

        public bool IsInternalizedPackage(UnityEditor.PackageManager.PackageInfo packageInfo)
        {
            var packageSource = packageInfo.source;
            switch (packageSource)
            {
                case PackageSource.Embedded:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.Embedded);
                case PackageSource.Registry:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.Registry);
                case PackageSource.BuiltIn:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.BuiltIn);
                case PackageSource.Unknown:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.Unknown);
                case PackageSource.Local:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.Local);
                case PackageSource.Git:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.Git);
#if UNITY_2019_3_OR_NEWER
                case PackageSource.LocalTarball:
                    return !m_projectGenerationFlag.HasFlag(ProjectGenerationFlag.LocalTarBall);
#endif
            }

            return false;
        }

        public UnityEditor.PackageManager.PackageInfo FindForAssetPath(string assetPath)
        {
            var parentPackageAssetPath = ResolvePotentialParentPackageAssetPath(assetPath);
            if (parentPackageAssetPath == null)
            {
                return null;
            }

            if (m_packageInfoCache.TryGetValue(parentPackageAssetPath, out var cachedPackageInfo))
            {
                return cachedPackageInfo;
            }

            var result = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(parentPackageAssetPath);
            m_packageInfoCache[parentPackageAssetPath] = result;
            return result;
        }

        public void ResetPackageInfoCache()
        {
            m_packageInfoCache.Clear();
        }

        public IEnumerable<string> GetAllAssetPaths()
        {
            return AssetDatabase.GetAllAssetPaths();
        }

        public string GetAssemblyNameFromScriptPath(string path)
        {
            return CompilationPipeline.GetAssemblyNameFromScriptPath(path);
        }

        public ResponseFileData ParseResponseFile(string responseFilePath, string projectDirectory, string[] systemReferenceDirectories)
        {
            return CompilationPipeline.ParseResponseFile(
                responseFilePath,
                projectDirectory,
                systemReferenceDirectories
            );
        }

        private static string ResolvePotentialParentPackageAssetPath(string assetPath)
        {
            const string packagesPrefix = "packages/";
            if (!assetPath.StartsWith(packagesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var followupSeparator = assetPath.IndexOf('/', packagesPrefix.Length);
            if (followupSeparator == -1)
            {
                return assetPath.ToLowerInvariant();
            }

            return assetPath.Substring(0, followupSeparator).ToLowerInvariant();
        }
    }
}