using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallActionFeedback
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    [MenuItem("Tools/Echo Runner/Install Action Feedback")]
    public static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        if (player == null || player.GetComponent<PlayerController>() == null)
            throw new InvalidOperationException(
                "Action feedback install failed: scene player is missing.");

        PlayerFeedbackController feedback =
            player.GetComponent<PlayerFeedbackController>();
        if (feedback == null)
            feedback = player.AddComponent<PlayerFeedbackController>();
        feedback.enabled = true;

        EditorUtility.SetDirty(feedback);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException(
                "Action feedback install failed: SampleScene was not saved.");
        AssetDatabase.SaveAssets();
        ValidateInstalledScene();
        Debug.Log("ACTION_FEEDBACK_INSTALL_OK");
    }

    public static void ValidateInstalledScene()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        PlayerFeedbackController[] feedback = player != null
            ? player.GetComponents<PlayerFeedbackController>()
            : Array.Empty<PlayerFeedbackController>();
        if (feedback.Length != 1 || !feedback[0].enabled)
            throw new InvalidOperationException(
                "Action feedback validation failed: expected one enabled bridge.");
        if (player.GetComponent<PlayerController>() == null)
            throw new InvalidOperationException(
                "Action feedback validation failed: authority is missing.");
        Debug.Log("ACTION_FEEDBACK_SCENE_OK scene=" + scene.path);
    }
}
