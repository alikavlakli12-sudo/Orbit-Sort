using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OrbitSort.Editor
{
    public static class PrototypeBuilder
    {
        private const string DefaultOutput =
            "Builds/macOS/Orbit Sort.app";

        public static void BuildMacQa()
        {
            string requestedOutput = Environment.GetEnvironmentVariable(
                "ORBIT_SORT_BUILD_PATH");
            string output = string.IsNullOrWhiteSpace(requestedOutput)
                ? Path.GetFullPath(DefaultOutput)
                : Path.GetFullPath(requestedOutput);

            string directory = Path.GetDirectoryName(output);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(
                    $"Invalid build output path '{output}'.");
            }

            Directory.CreateDirectory(directory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/OrbitSort/Scenes/Prototype.unity"
                },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Orbit Sort QA build failed with "
                    + $"{summary.totalErrors} error(s).");
            }

            Debug.Log(
                $"Orbit Sort QA build created at '{output}' "
                + $"({summary.totalSize} bytes).");
        }
    }
}
