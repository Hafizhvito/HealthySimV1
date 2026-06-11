using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntroReplayFixSceneSetup
{
    [MenuItem("HealthySim/Fix Intro Replay On Return")]
    public static void FixIntroReplayFlags()
    {
        int changed = 0;

        SampleSceneBootstrap[] bootstraps = Object.FindObjectsByType<SampleSceneBootstrap>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < bootstraps.Length; i++)
        {
            SampleSceneBootstrap bootstrap = bootstraps[i];
            if (bootstrap == null)
                continue;

            SerializedObject so = new SerializedObject(bootstrap);
            SerializedProperty forceIntro = so.FindProperty("forceIntroEveryPlay");
            if (forceIntro != null && forceIntro.boolValue)
            {
                forceIntro.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bootstrap);
                changed++;
            }
        }

        StoryIntroManager[] intros = Object.FindObjectsByType<StoryIntroManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < intros.Length; i++)
        {
            StoryIntroManager intro = intros[i];
            if (intro == null)
                continue;

            SerializedObject so = new SerializedObject(intro);
            SerializedProperty forcePlay = so.FindProperty("forcePlayIntro");
            if (forcePlay != null && forcePlay.boolValue)
            {
                forcePlay.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(intro);
                changed++;
            }
        }

        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[HealthySim] Intro replay flags updated ({changed} component(s)). Save scene (Ctrl+S).");
    }
}
