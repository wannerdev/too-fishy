#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TooFishy.EditorTools
{
    /// <summary>
    /// Command-line builds used by CI and local release builds.
    ///
    /// Android APK:
    ///   Unity.exe -batchmode -nographics -quit -projectPath unity
    ///             -executeMethod TooFishy.EditorTools.BuildScripts.BuildAndroid
    ///             [-outputPath build/android/too-fishy.apk] [-versionCode 2]
    ///
    /// WebGL:
    ///   Unity.exe -batchmode -nographics -quit -projectPath unity
    ///             -executeMethod TooFishy.EditorTools.BuildScripts.BuildWebGL
    ///             [-outputPath build/webgl]
    ///
    /// Android release signing is read from the same environment variables the other wannerdev
    /// apps use: KEYSTORE_FILE, KEYSTORE_PASSWORD, KEY_ALIAS, KEY_PASSWORD. Without them the
    /// APK is debug-signed so it can still be installed for testing.
    /// </summary>
    public static class BuildScripts
    {
        const string DefaultApk = "build/android/too-fishy.apk";
        const string DefaultAab = "build/android/too-fishy.aab";
        /// <summary>Google Play: new apps and updates must target API 36 from 2026-08-31.</summary>
        const int TargetApi = 36;
        const string DefaultWeb = "build/webgl";
        const string BundleId = "de.zackig.toofishy";
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("Too Fishy/Build Android APK")]
        public static void BuildAndroidFromMenu() => BuildAndroidInternal(DefaultApk, null, false, false);

        [MenuItem("Too Fishy/Build WebGL")]
        public static void BuildWebGLFromMenu() => BuildWebGLInternal(DefaultWeb, false);

        public static void BuildAndroid()
        {
            string output = GetArg("-outputPath") ?? DefaultApk;
            int? versionCode = int.TryParse(GetArg("-versionCode"), out int parsed) ? parsed : null;
            BuildAndroidInternal(output, versionCode, true, false);
        }

        /// <summary>Release App Bundle for Google Play. Refuses to run without a release keystore in the environment.</summary>
        public static void BuildAndroidBundle()
        {
            string output = GetArg("-outputPath") ?? DefaultAab;
            int? versionCode = int.TryParse(GetArg("-versionCode"), out int parsed) ? parsed : null;
            BuildAndroidInternal(output, versionCode, true, true);
        }

        public static void BuildWebGL()
        {
            BuildWebGLInternal(GetArg("-outputPath") ?? DefaultWeb, true);
        }

        static void BuildAndroidInternal(string outputPath, int? versionCode, bool exitOnFailure, bool bundle)
        {
            GodotAssetSync.TargetOverride = BuildTarget.Android;
            ApplySharedPlayerSettings();
            ConfigureAndroidPlayerSettings(versionCode);
            EditorUserBuildSettings.buildAppBundle = bundle;
            if (!ConfigureAndroidSigning() && bundle)
            {
                Debug.LogError("[Build] A Play bundle needs KEYSTORE_FILE, KEYSTORE_PASSWORD and KEY_ALIAS (and optionally KEY_PASSWORD) in the environment.");
                if (exitOnFailure) EditorApplication.Exit(1);
                return;
            }

            string fullPath = AbsoluteProjectPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = fullPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[Build] Android {(bundle ? "AAB" : "APK")} -> {fullPath} (targetSdk {(int)PlayerSettings.Android.targetSdkVersion}, versionCode {PlayerSettings.Android.bundleVersionCode}, " +
                      $"version {PlayerSettings.bundleVersion}, customKeystore={PlayerSettings.Android.useCustomKeystore})");
            Run(options, exitOnFailure);
        }

        static void BuildWebGLInternal(string outputDir, bool exitOnFailure)
        {
            GodotAssetSync.TargetOverride = BuildTarget.WebGL;
            ApplySharedPlayerSettings();

            // Gzip with the JavaScript decompression fallback works on any static host without
            // special Content-Encoding configuration (Caddy, nginx, GitHub Pages, Coolify static).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.template = "PROJECT:TooFishy";
            if (!Directory.Exists(AbsoluteProjectPath("Assets/WebGLTemplates/TooFishy")))
                PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.runInBackground = true;

            string fullPath = AbsoluteProjectPath(outputDir);
            Directory.CreateDirectory(fullPath);

            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = fullPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[Build] WebGL -> {fullPath} (template {PlayerSettings.WebGL.template})");
            Run(options, exitOnFailure);
        }

        public static void BuildWindows()
        {
            GodotAssetSync.TargetOverride = BuildTarget.StandaloneWindows64;
            ApplySharedPlayerSettings();
            string fullPath = AbsoluteProjectPath(GetArg("-outputPath") ?? "build/windows/TooFishy.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = fullPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            Debug.Log($"[Build] Windows -> {fullPath}");
            Run(options, true);
        }

        static void Run(BuildPlayerOptions options, bool exitOnFailure)
        {
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Build] Succeeded: {summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F0} s -> {options.locationPathName}");
                return;
            }

            Debug.LogError($"[Build] Failed: {summary.result}, {summary.totalErrors} errors");
            if (exitOnFailure) EditorApplication.Exit(1);
        }

        static void ApplySharedPlayerSettings()
        {
            // The game uses the original Godot meshes/textures, copied in on demand.
            GodotAssetSync.EnsureSynced();
            // Godot renders in linear space; gamma would wash out the textures and fog.
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "wannerdev";
            PlayerSettings.productName = "Too Fishy";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.stripEngineCode = true;
        }

        static void ConfigureAndroidPlayerSettings(int? versionCode)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            if (versionCode.HasValue) PlayerSettings.Android.bundleVersionCode = versionCode.Value;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

            // Google Play requires 64-bit; IL2CPP is the only backend that supports ARM64.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApi;
            string versionName = GetArg("-versionName");
            if (!string.IsNullOrWhiteSpace(versionName)) PlayerSettings.bundleVersion = versionName;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
        }

        /// <summary>Returns true when a release keystore was configured. Never logs the credentials.</summary>
        static bool ConfigureAndroidSigning()
        {
            string keystore = Environment.GetEnvironmentVariable("KEYSTORE_FILE");
            string storePass = Environment.GetEnvironmentVariable("KEYSTORE_PASSWORD");
            string alias = Environment.GetEnvironmentVariable("KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("KEY_PASSWORD");

            if (string.IsNullOrWhiteSpace(keystore) || !File.Exists(keystore) ||
                string.IsNullOrWhiteSpace(storePass) || string.IsNullOrWhiteSpace(alias))
            {
                Debug.LogWarning("[Build] No release keystore in the environment; producing a debug-signed APK.");
                PlayerSettings.Android.useCustomKeystore = false;
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrWhiteSpace(keyPass) ? storePass : keyPass;
            return true;
        }

        static string AbsoluteProjectPath(string relative) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }
    }
}
#endif
