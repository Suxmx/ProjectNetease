using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MemoFramework.Editor.Dependencies
{
    [InitializeOnLoad]
    internal static class DependencyInstaller
    {
        private const string AutoPromptSessionKey = "MemoFramework.Setup.AutoPrompted";
        private const string RelocationSourceKey = "MemoFramework.Setup.Relocation.Source";
        private const string RelocationRootKey = "MemoFramework.Setup.Relocation.Root";
        private const string RelocationNameKey = "MemoFramework.Setup.Relocation.Name";

        static DependencyInstaller()
        {
            EditorApplication.delayCall += AutoPromptIfRequired;
            EditorApplication.delayCall += ResumePendingRelocation;
        }

        [MenuItem("Tools/MemoFramework/Setup")]
        public static void OpenSetupWindow()
        {
            MemoFrameworkSetupWindow.Open();
        }

        private static void AutoPromptIfRequired()
        {
            if (SessionState.GetBool(AutoPromptSessionKey, false))
                return;

            SessionState.SetBool(AutoPromptSessionKey, true);
            MemoFrameworkSetupWindow.OpenIfCoreDependenciesAreMissing();
        }

        /// <summary>
        /// 记录待重定位的可选包信息到 SessionState（跨域重载存活），并开始轮询等待导入完成后执行移动。
        /// </summary>
        internal static void StartPendingRelocation(string displayName, string sourcePath, string rootPath)
        {
            SessionState.SetString(RelocationNameKey, displayName);
            SessionState.SetString(RelocationSourceKey, sourcePath);
            SessionState.SetString(RelocationRootKey, rootPath);
            EditorApplication.update -= PollPendingRelocation;
            EditorApplication.update += PollPendingRelocation;
        }

        /// <summary>
        /// 域重载后检查是否有未完成的重定位任务，有则恢复轮询。
        /// </summary>
        private static void ResumePendingRelocation()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(RelocationSourceKey, string.Empty)))
                return;
            EditorApplication.update -= PollPendingRelocation;
            EditorApplication.update += PollPendingRelocation;
        }

        /// <summary>
        /// 轮询等待编辑器空闲且源文件夹存在后，将可选包移动到统一安装目录。
        /// </summary>
        private static void PollPendingRelocation()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var sourcePath = SessionState.GetString(RelocationSourceKey, string.Empty);
            if (string.IsNullOrEmpty(sourcePath))
            {
                EditorApplication.update -= PollPendingRelocation;
                return;
            }

            // 源文件夹尚未出现，说明导入还在进行中。
            if (!AssetDatabase.IsValidFolder(sourcePath))
                return;

            EditorApplication.update -= PollPendingRelocation;

            var rootPath = SessionState.GetString(RelocationRootKey, string.Empty);
            var displayName = SessionState.GetString(RelocationNameKey, string.Empty);

            SessionState.EraseString(RelocationSourceKey);
            SessionState.EraseString(RelocationRootKey);
            SessionState.EraseString(RelocationNameKey);

            RelocateImportedPackage(displayName, sourcePath, rootPath);
        }

        private static void RelocateImportedPackage(string displayName, string sourcePath, string rootPath)
        {
            if (AssetDatabase.IsValidFolder(rootPath))
            {
                Debug.Log($"[MemoFramework] {displayName} is already installed at {rootPath}.");
                return;
            }

            var parentPath = Path.GetDirectoryName(rootPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parentPath))
                EnsureFolderExists(parentPath);

            var error = AssetDatabase.MoveAsset(sourcePath, rootPath);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogWarning($"[MemoFramework] Imported {displayName} but could not relocate to '{rootPath}': {error}");
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[MemoFramework] {displayName} installed to {rootPath}.");

            foreach (var window in UnityEngine.Object.FindObjectsByType<MemoFrameworkSetupWindow>(FindObjectsSortMode.None))
                window.Repaint();
        }

        /// <summary>
        /// 递归确保指定的 AssetDatabase 文件夹路径存在，不存在则逐级创建。
        /// </summary>
        private static void EnsureFolderExists(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            var parentPath = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parentPath) && !AssetDatabase.IsValidFolder(parentPath))
                EnsureFolderExists(parentPath);

            if (string.IsNullOrEmpty(parentPath) || !AssetDatabase.IsValidFolder(parentPath))
                return;

            AssetDatabase.CreateFolder(parentPath, Path.GetFileName(assetFolderPath));
        }
    }

    internal sealed class MemoFrameworkSetupWindow : EditorWindow
    {
        private const string WindowTitle = "MemoFramework Setup";
        /// <summary>跨域重载存活的标志：为 true 表示核心依赖安装流程未完成，需要在窗口重建后自动续装。</summary>
        private const string InstallingFlagKey = "MemoFramework.Setup.Installing";

        private static readonly PackageDependency[] CoreDependencies =
        {
            new PackageDependency("Settings Manager", "com.unity.settings-manager", "com.unity.settings-manager@2.0.1"),
            new PackageDependency("Cinemachine", "com.unity.cinemachine", "com.unity.cinemachine@3.1.7")
        };

        private const string InstallRoot = "Assets/MemoFramework/InstalledOptionalPackage";

        private static readonly OptionalPackage[] OptionalPackages =
        {
            new OptionalPackage(
                "MemoFramework_Net",
                "Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Net",
                "Assets/MemoFramework_Net",
                "Assets/MemoFramework/OptionalPackages/MemoFramework_Net.unitypackage",
                string.Empty),
            new OptionalPackage(
                "MemoFramework_Skill",
                "Assets/MemoFramework/InstalledOptionalPackage/MemoFramework_Skill",
                "Assets/MemoFramework_Skill",
                "Assets/MemoFramework/OptionalPackages/MemoFramework_Skill.unitypackage",
                "导入后会自动生成技能序列化代码。")
        };

        private static ListRequest autoPromptRequest;

        private readonly HashSet<string> installedPackageNames = new HashSet<string>();
        private Queue<PackageDependency> pendingInstalls;
        private PackageDependency currentPendingDependency;
        private ListRequest listRequest;
        private AddRequest addRequest;
        private Vector2 scrollPosition;
        private string statusMessage = "Checking packages...";
        private bool isChecking;
        private bool isInstalling;
        private bool hasPackageList;

        public static void Open()
        {
            var window = GetWindow<MemoFrameworkSetupWindow>(true, WindowTitle);
            window.minSize = new Vector2(480f, 360f);
            window.RefreshPackageList();
            window.Show();
        }

        public static void OpenIfCoreDependenciesAreMissing()
        {
            try
            {
                autoPromptRequest = Client.List(true, true);
                EditorApplication.update -= WatchAutoPromptRequest;
                EditorApplication.update += WatchAutoPromptRequest;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"MemoFramework could not check package dependencies: {exception.Message}");
                Open();
            }
        }

        private static void WatchAutoPromptRequest()
        {
            if (!autoPromptRequest.IsCompleted)
                return;

            EditorApplication.update -= WatchAutoPromptRequest;

            if (autoPromptRequest.Status != StatusCode.Success)
            {
                Debug.LogWarning($"MemoFramework could not check package dependencies: {autoPromptRequest.Error.message}");
                Open();
                return;
            }

            var installedPackages = new HashSet<string>();
            foreach (var packageInfo in autoPromptRequest.Result)
            {
                installedPackages.Add(packageInfo.name);
            }

            foreach (var dependency in CoreDependencies)
            {
                if (!installedPackages.Contains(dependency.PackageName))
                {
                    Open();
                    return;
                }
            }
        }

        private void OnEnable()
        {
            // 域重载或重新打开窗口后，之前未完成的 Package Manager 请求已经失效，
            // 直接复位标志位，避免 isChecking/isInstalling 卡住导致面板全灰。
            isChecking = false;
            isInstalling = false;
            RefreshPackageList();
        }

        private void OnDisable()
        {
            EditorApplication.update -= WatchListRequest;
            EditorApplication.update -= WatchAddRequest;
            EditorApplication.update -= WaitForIdleBeforeList;
            EditorApplication.update -= WaitForIdleBeforeAdd;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("MemoFramework Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawCoreDependencies();
            EditorGUILayout.Space(14f);
            DrawOptionalPackages();
            EditorGUILayout.Space(14f);
            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        private void DrawCoreDependencies()
        {
            EditorGUILayout.LabelField("Core Dependencies", EditorStyles.boldLabel);

            foreach (var dependency in CoreDependencies)
            {
                DrawDependencyRow(dependency);
            }

            using (new EditorGUI.DisabledScope(isChecking || isInstalling || !HasMissingCoreDependencies()))
            {
                if (GUILayout.Button("Setup Core Dependencies", GUILayout.Height(28f)))
                {
                    InstallMissingCoreDependencies();
                }
            }
        }

        private void DrawDependencyRow(PackageDependency dependency)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(dependency.DisplayName, GUILayout.Width(150f));
            EditorGUILayout.LabelField(dependency.PackageId);

            var label = installedPackageNames.Contains(dependency.PackageName) ? "Installed" : "Missing";
            GUILayout.Label(label, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOptionalPackages()
        {
            EditorGUILayout.LabelField("Optional Packages", EditorStyles.boldLabel);

            foreach (var optionalPackage in OptionalPackages)
            {
                DrawOptionalPackage(optionalPackage);
                EditorGUILayout.Space(4f);
            }
        }

        private void DrawOptionalPackage(OptionalPackage optionalPackage)
        {
            var installed = AssetDatabase.IsValidFolder(optionalPackage.RootPath);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(optionalPackage.DisplayName, GUILayout.Width(150f));
            EditorGUILayout.LabelField(installed ? "Installed" : "Not installed");
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(optionalPackage.Description))
                EditorGUILayout.HelpBox(optionalPackage.Description, MessageType.None);

            using (new EditorGUI.DisabledScope(installed || isInstalling || isChecking))
            {
                if (File.Exists(optionalPackage.BundledAssetPath) && GUILayout.Button($"Import Bundled {optionalPackage.DisplayName}", GUILayout.Height(26f)))
                {
                    ImportOptionalPackage(optionalPackage, optionalPackage.BundledAssetPath);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        private void RefreshPackageList()
        {
            if (isChecking)
                return;

            isChecking = true;
            hasPackageList = false;
            statusMessage = "Checking packages...";
            // 在编译/导入期间调用 Client.List 会导致请求永不完成（面板卡灰），
            // 因此先轮询等待编辑器空闲，再发起请求。
            EditorApplication.update -= WaitForIdleBeforeList;
            EditorApplication.update += WaitForIdleBeforeList;
            Repaint();
        }

        /// <summary>
        /// 等待编辑器结束编译与资源导入后，再发起 Client.List 请求。
        /// </summary>
        private void WaitForIdleBeforeList()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= WaitForIdleBeforeList;

            try
            {
                listRequest = Client.List(true, true);
                EditorApplication.update -= WatchListRequest;
                EditorApplication.update += WatchListRequest;
            }
            catch (Exception exception)
            {
                isChecking = false;
                statusMessage = $"Could not check packages: {exception.Message}";
                Repaint();
            }
        }

        private void WatchListRequest()
        {
            if (!listRequest.IsCompleted)
                return;

            EditorApplication.update -= WatchListRequest;
            isChecking = false;

            if (listRequest.Status != StatusCode.Success)
            {
                hasPackageList = false;
                statusMessage = $"Could not check packages: {listRequest.Error.message}";
                Repaint();
                return;
            }

            installedPackageNames.Clear();
            foreach (var packageInfo in listRequest.Result)
            {
                installedPackageNames.Add(packageInfo.name);
            }

            hasPackageList = true;
            statusMessage = HasMissingCoreDependencies()
                ? "Core dependencies are missing. Click Setup Core Dependencies to install them."
                : "Core dependencies are installed.";

            // 域重载后窗口重建，若安装流程未完成且仍有缺失，自动续装剩余依赖。
            if (SessionState.GetBool(InstallingFlagKey, false) && HasMissingCoreDependencies())
            {
                InstallMissingCoreDependencies();
            }
            Repaint();
        }

        private bool HasMissingCoreDependencies()
        {
            if (!hasPackageList)
                return true;

            foreach (var dependency in CoreDependencies)
            {
                if (!installedPackageNames.Contains(dependency.PackageName))
                    return true;
            }

            return false;
        }

        private void InstallMissingCoreDependencies()
        {
            pendingInstalls = new Queue<PackageDependency>();

            foreach (var dependency in CoreDependencies)
            {
                if (!installedPackageNames.Contains(dependency.PackageName))
                    pendingInstalls.Enqueue(dependency);
            }

            if (pendingInstalls.Count == 0)
            {
                statusMessage = "Core dependencies are installed.";
                SessionState.EraseBool(InstallingFlagKey);
                return;
            }

            isInstalling = true;
            SessionState.SetBool(InstallingFlagKey, true);
            AddNextPackage();
        }

        private void AddNextPackage()
        {
            if (pendingInstalls.Count == 0)
            {
                isInstalling = false;
                SessionState.EraseBool(InstallingFlagKey);
                statusMessage = "Core dependency setup finished.";
                RefreshPackageList();
                return;
            }

            currentPendingDependency = pendingInstalls.Dequeue();
            statusMessage = $"Installing {currentPendingDependency.DisplayName}...";
            // 上一个包安装可能已触发编译，此时立即调用 Client.Add 会卡住，先等待空闲。
            EditorApplication.update -= WaitForIdleBeforeAdd;
            EditorApplication.update += WaitForIdleBeforeAdd;
            Repaint();
        }

        /// <summary>
        /// 等待编辑器结束编译与资源导入后，再发起 Client.Add 请求安装下一个核心依赖。
        /// </summary>
        private void WaitForIdleBeforeAdd()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= WaitForIdleBeforeAdd;

            try
            {
                addRequest = Client.Add(currentPendingDependency.PackageId);
                EditorApplication.update -= WatchAddRequest;
                EditorApplication.update += WatchAddRequest;
            }
            catch (Exception exception)
            {
                isInstalling = false;
                statusMessage = $"Package install failed: {exception.Message}";
                Repaint();
            }
        }

        private void WatchAddRequest()
        {
            if (!addRequest.IsCompleted)
                return;

            EditorApplication.update -= WatchAddRequest;

            if (addRequest.Status != StatusCode.Success)
            {
                isInstalling = false;
                SessionState.EraseBool(InstallingFlagKey);
                statusMessage = $"Package install failed: {addRequest.Error.message}";
                Repaint();
                return;
            }

            installedPackageNames.Add(addRequest.Result.name);
            AddNextPackage();
        }

        private void ImportOptionalPackage(OptionalPackage optionalPackage, string packagePath)
        {
            if (!File.Exists(packagePath))
            {
                statusMessage = $"{optionalPackage.DisplayName} was not found.";
                return;
            }

            statusMessage = $"Importing {optionalPackage.DisplayName}...";
            // 含脚本的包导入后会触发域重载，实例回调和字段都会丢失，
            // 因此把重定位任务交给 DependencyInstaller 静态类用 SessionState 跨域重载轮询执行。
            DependencyInstaller.StartPendingRelocation(
                optionalPackage.DisplayName,
                optionalPackage.ImportedSourcePath,
                optionalPackage.RootPath);
            AssetDatabase.ImportPackage(packagePath, false);
        }

        private readonly struct OptionalPackage
        {
            public readonly string DisplayName;
            /// <summary>最终安装位置，用于判断是否已安装。</summary>
            public readonly string RootPath;
            /// <summary>unitypackage 解压后默认落在 Assets 根目录下的路径，需随后移动到 <see cref="RootPath"/>。</summary>
            public readonly string ImportedSourcePath;
            public readonly string BundledAssetPath;
            public readonly string Description;

            public OptionalPackage(string displayName, string rootPath, string importedSourcePath, string bundledAssetPath, string description)
            {
                DisplayName = displayName;
                RootPath = rootPath;
                ImportedSourcePath = importedSourcePath;
                BundledAssetPath = bundledAssetPath;
                Description = description;
            }
        }

        private readonly struct PackageDependency
        {
            public readonly string DisplayName;
            public readonly string PackageName;
            public readonly string PackageId;

            public PackageDependency(string displayName, string packageName, string packageId)
            {
                DisplayName = displayName;
                PackageName = packageName;
                PackageId = packageId;
            }
        }
    }
}
