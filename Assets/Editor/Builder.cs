using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds the game. Runs from the live Editor so it never fights the project lock
/// that a separate batch-mode Editor would take.
/// </summary>
public static class Builder
{
    const string SceneGuidPath = "Assets/Scenes/SampleScene.unity";

    static string[] Scenes()
    {
        // Make sure the scene is actually in the build, or the player boots to nothing.
        if (!File.Exists(SceneGuidPath))
            throw new Exception("Scene not found at " + SceneGuidPath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneGuidPath, true) };
        return new[] { SceneGuidPath };
    }

    [MenuItem("Laundry Monster/Build Windows")]
    public static string BuildWindows()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows");
        Directory.CreateDirectory(dir);

        var options = new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = Path.Combine(dir, "LaundryMonster.exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        return Run(options);
    }

    [MenuItem("Laundry Monster/Build WebGL")]
    public static string BuildWebGL()
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "WebGL");
        Directory.CreateDirectory(dir);

        // Keep the download small: this is the target platform.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.SetManagedStrippingLevel(
            UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

        var options = new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = dir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        return Run(options);
    }

    static string Run(BuildPlayerOptions options)
    {
        PlayerSettings.productName = "Laundry Monster";
        PlayerSettings.companyName = "Cory Cowgill";
        PlayerSettings.runInBackground = true;

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        var msg = "result=" + summary.result
                + " target=" + summary.platform
                + " sizeMB=" + (summary.totalSize / (1024f * 1024f)).ToString("0.0")
                + " time=" + summary.totalTime.TotalSeconds.ToString("0") + "s"
                + " errors=" + summary.totalErrors
                + " warnings=" + summary.totalWarnings
                + " out=" + summary.outputPath;

        // Leave a breadcrumb on disk: the eval call that started this will have
        // timed out long before the build finishes.
        try
        {
            File.WriteAllText(
                Path.Combine(Directory.GetCurrentDirectory(), "Builds", "last-build.txt"),
                DateTime.Now + "\n" + msg + "\n");
        }
        catch { }

        if (summary.result != BuildResult.Succeeded)
            throw new Exception("BUILD FAILED: " + msg);

        return msg;
    }
}
