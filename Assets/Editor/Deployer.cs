using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// One menu item that builds the WebGL player and publishes it to the `deploy` branch,
/// which Render serves.
///
/// The publishing itself stays in publish-deploy.sh rather than being reimplemented in
/// C#: that script is also what you run by hand, and two implementations of the same
/// git dance would eventually disagree about something that matters.
/// </summary>
public static class Deployer
{
    const string Script = "publish-deploy.sh";

    [MenuItem("Laundry Monster/Build and Deploy WebGL", false, 20)]
    public static void BuildAndDeploy()
    {
        if (!EditorUtility.DisplayDialog(
                "Build and deploy",
                "Build the WebGL player and push it to the 'deploy' branch?\n\n"
                + "Render redeploys automatically from that branch, so this publishes "
                + "the game publicly.",
                "Build and deploy", "Cancel"))
            return;

        string output;
        try
        {
            output = Builder.BuildWebGL();
        }
        catch (Exception e)
        {
            Debug.LogError("[Deploy] build failed, nothing was published: " + e.Message);
            return;
        }
        Debug.Log("[Deploy] built -> " + output);

        Publish();
    }

    [MenuItem("Laundry Monster/Deploy Last Build", false, 21)]
    public static void Publish()
    {
        var repo = Directory.GetCurrentDirectory();
        var script = Path.Combine(repo, Script);
        if (!File.Exists(script))
        {
            Debug.LogError("[Deploy] " + Script + " not found at the project root.");
            return;
        }

        var bash = FindBash();
        if (bash == null)
        {
            Debug.LogError("[Deploy] no bash found. Run this from a terminal instead:\n"
                         + "    ./" + Script);
            return;
        }

        var psi = new ProcessStartInfo(bash, "\"" + script + "\"")
        {
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using (var proc = Process.Start(psi))
        {
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
                Debug.Log("[Deploy] " + stdout.Trim());
            else
                Debug.LogError("[Deploy] publish failed (exit " + proc.ExitCode + ")\n"
                               + stdout + "\n" + stderr);
        }
    }

    /// <summary>
    /// Locate bash. On Windows this is Git for Windows, which is already installed
    /// wherever this repo was cloned with git.
    /// </summary>
    static string FindBash()
    {
        var candidates = new[]
        {
            "/bin/bash",
            "/usr/bin/bash",
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Programs\Git\bin\bash.exe"),
        };

        foreach (var c in candidates)
            if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;

        // Fall back to whatever is on PATH.
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var name in new[] { "bash.exe", "bash" })
            {
                try
                {
                    var full = Path.Combine(dir, name);
                    if (File.Exists(full)) return full;
                }
                catch (ArgumentException) { /* malformed PATH entry */ }
            }
        }
        return null;
    }
}
