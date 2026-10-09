#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
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
        const string DefaultWeb = "build/webgl";
        const string BundleId = "de.wannerdev.toofishy";
        static readonly string[] Scenes = { "Assets/Scenes/Main.unity" };

        [MenuItem("Too Fishy/Build Android APK")]
        public static void BuildAndroidFromMenu() => BuildAndroidInternal(DefaultApk, null, false);

        [MenuItem("Too Fishy/Build WebGL")]
        public static void BuildWebGLFromMenu() => BuildWebGLInternal(DefaultWeb, false);

        public static void BuildAndroid()
        {
            string output = GetArg("-outputPath") ?? DefaultApk;
            int? versionCode = int.TryParse(GetArg("-versionCode"), out int parsed) ? parsed : null;
            BuildAndroidInternal(output, versionCode, true);
        }

        public static void BuildWebGL()
        {
            BuildWebGLInternal(GetArg("-outputPath") ?? DefaultWeb, true);
        }

        static void BuildAndroidInternal(string outputPath, int? versionCode, bool exitOnFailure)
        {
            ApplySharedPlayerSettings();
            ConfigureAndroidPlayerSettings(versionCode);
            ConfigureAndroidSigning();

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

            Debug.Log($"[Build] Android -> {fullPath} (versionCode {PlayerSettings.Android.bundleVersionCode}, " +
                      $"version {PlayerSettings.bundleVersion}, customKeystore={PlayerSettings.Android.useCustomKeystore})");
            Run(options, exitOnFailure);
        }

        static void BuildWebGLInternal(string outputDir, bool exitOnFailure)
        {
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
            PlayerSettings.companyName = "wannerdev";
            PlayerSettings.productName = "Too Fishy";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.stripEngineCode = true;
        }

        static void ConfigureAndroidPlayerSettings(int? versionCode)
        {
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
            if (versionCode.HasValue) PlayerSettings.Android.bundleVersionCode = versionCode.Value;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

            // Google Play requires 64-bit; IL2CPP is the only backend that supports ARM64.
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // Auto = the highest SDK installed with the editor (35 with 2022.3.50f1)
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        static void ConfigureAndroidSigning()
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
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = string.IsNullOrWhiteSpace(keyPass) ? storePass : keyPass;
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
