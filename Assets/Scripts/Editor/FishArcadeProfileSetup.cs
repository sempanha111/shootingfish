#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Compatibility entry point that replaces the obsolete profile builder
/// from earlier packages. Keeping this filename prevents stale editor code
/// from referencing removed enum values or profile APIs after import.
/// </summary>
public static class FishArcadeProfileSetup
{
    [MenuItem("Tools/Fish Arcade/Build Professional Arcade Profiles")]
    public static void BuildProfessionalProfiles()
    {
        FishArcade46Setup.ApplyCompleteSetupFromSelection();
    }
}
#endif
