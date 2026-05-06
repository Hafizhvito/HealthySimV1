using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader
{
    private const string LOADING_SCENE = "LoadingScreen";

    // Panggil ini dari scene manapun untuk pindah scene
    public static void LoadScene(string sceneName)
    {
        LoadingManager.TargetScene = sceneName;
        SceneManager.LoadScene(LOADING_SCENE);
    }
}
