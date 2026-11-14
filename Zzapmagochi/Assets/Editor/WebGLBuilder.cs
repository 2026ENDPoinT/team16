using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch-mode WebGL build entry point.
/// Usage:
///   Unity.exe -quit -batchmode -nographics -projectPath &lt;proj&gt; \
///             -executeMethod WebGLBuilder.Build [-buildOutput &lt;dir&gt;]
/// </summary>
public static class WebGLBuilder
{
    const string DefaultOutput = "Build/WebGL";

    public static void Build()
    {
        string output = GetArg("-buildOutput") ?? DefaultOutput;

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[WebGLBuilder] No enabled scenes in Build Settings.");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[WebGLBuilder] Scenes: {string.Join(", ", scenes)}");
        Debug.Log($"[WebGLBuilder] Output: {output}");

        // IL2CPP is the only scripting backend for WebGL.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        // No compression, so the build can be served by any plain static file server
        // (Brotli/Gzip need Content-Encoding headers configured).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log($"[WebGLBuilder] Result={summary.result} " +
                  $"Size={summary.totalSize / (1024 * 1024)}MB " +
                  $"Time={summary.totalTime} " +
                  $"Errors={summary.totalErrors} Warnings={summary.totalWarnings}");

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string GetArg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
