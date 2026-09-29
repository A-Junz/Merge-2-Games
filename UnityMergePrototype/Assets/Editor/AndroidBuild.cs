using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MergePrototype.EditorTools
{
    /// <summary>用于命令行生成可安装的 Android APK。</summary>
    public static class AndroidBuild
    {
        public static void BuildApk()
        {
            var outputPath = GetBuildOutputPath();
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            EditorUserBuildSettings.buildAppBundle = false;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainSceneBuilder.ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Android APK 构建失败：{report.summary.result}，错误数 {report.summary.totalErrors}");
            }

            Debug.Log($"[AndroidBuild] APK 已生成：{report.summary.outputPath}，大小 {report.summary.totalSize} bytes");
        }

        private static string GetBuildOutputPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildOutput")
                {
                    return args[i + 1];
                }
            }

            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "output", "UnityMergePrototype.apk");
        }
    }
}
