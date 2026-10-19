using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Historical installers overwrite the accepted runner. Keep them callable for
// deliberate reconstruction without exposing a one-click rollback in the Editor.
public static class LegacyRunnerInstallGuard
{
    public static bool Allow(string entryPoint)
    {
        string optIn = "-allowLegacyRunnerInstall=" + entryPoint;
        if (Application.isBatchMode && HasExactOptIn(Environment.GetCommandLineArgs(), entryPoint))
            return true;

        string message = entryPoint + " is a historical installer and can replace the accepted "
            + "player model, materials or animation controller. No changes were made. "
            + "Use an isolated checkout and explicitly run this batch entry point with " + optIn
            + " only when reconstructing that historical version.";
        if (Application.isBatchMode)
            throw new InvalidOperationException(message);

        EditorUtility.DisplayDialog("Legacy runner installer blocked", message, "Close");
        return false;
    }

    private static bool HasExactOptIn(string[] arguments, string entryPoint)
    {
        return arguments != null && arguments.Contains(
            "-allowLegacyRunnerInstall=" + entryPoint, StringComparer.Ordinal);
    }
}
