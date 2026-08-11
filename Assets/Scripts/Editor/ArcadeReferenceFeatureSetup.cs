#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArcadeReferenceFeatureSetup
{
    [MenuItem(
        "Tools/Shooting Fish/Install Reference-Style Power Skills"
    )]
    private static void InstallControllers()
    {
        GameManager gameManager =
            Object.FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError(
                "Open the gameplay scene first. GameManager must " +
                "exist before installing the power-skill controller."
            );
            return;
        }

        ArcadePowerSkillController skills =
            gameManager.GetComponent<ArcadePowerSkillController>();

        if (skills == null)
        {
            skills = Undo.AddComponent<ArcadePowerSkillController>(
                gameManager.gameObject
            );
        }

        EditorUtility.SetDirty(gameManager);
        EditorUtility.SetDirty(skills);
        EditorSceneManager.MarkSceneDirty(
            gameManager.gameObject.scene
        );
        Selection.activeGameObject = gameManager.gameObject;

        Debug.Log(
            "Reference-style power-skill controller installed. " +
            "Assign optional button UI, effects, and audio by following " +
            "REFERENCE_STYLE_ARCADE_FEATURES_SETUP.md.",
            gameManager
        );
    }

    [MenuItem(
        "Tools/Shooting Fish/Install Reference-Style Power Skills",
        true
    )]
    private static bool ValidateInstallControllers()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
