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

        // Minimal, not High. The room is built from code-created materials, and
        // aggressive stripping removes shaders nothing in the scene statically
        // references. 11.8MB is already fine for this game; correctness wins.
        PlayerSettings.SetManagedStrippingLevel(
            UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);

        EnsureShadersIncluded();

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

    /// <summary>
    /// Force the shaders this game needs into the build. Everything here is applied to
    /// materials created at runtime or by an editor script, so the build-time stripper
    /// sees nothing referencing them and throws them away - which is how you end up
    /// with magenta quads and invisible geometry in a player that looks fine in the Editor.
    /// </summary>
    static void EnsureShadersIncluded()
    {
        var wanted = new[]
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Simple Lit",
        };

        // GraphicsSettings.asset is not an AssetDatabase path; this is the supported handle.
        var graphics = UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings();
        if (graphics == null) { Debug.LogWarning("Builder: no GraphicsSettings object."); return; }

        var so = new SerializedObject(graphics);
        var list = so.FindProperty("m_AlwaysIncludedShaders");

        var have = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var s = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (s != null) have.Add(s.name);
        }

        foreach (var name in wanted)
        {
            if (have.Contains(name)) continue;
            var shader = Shader.Find(name);
            if (shader == null) { Debug.LogWarning("Builder: shader not found: " + name); continue; }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            Debug.Log("Builder: always-including shader " + name);
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    static string Run(BuildPlayerOptions options)
    {
        EnsureShadersIncluded();

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
