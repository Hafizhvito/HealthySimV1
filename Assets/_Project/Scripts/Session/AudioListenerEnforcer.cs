using UnityEngine;

using UnityEngine.SceneManagement;



/// <summary>

/// Keeps exactly one enabled <see cref="AudioListener"/> on an active GameObject.

/// Never disables listeners until a replacement is confirmed active.

/// </summary>

public static class AudioListenerEnforcer

{

    public static bool HasActiveListener()

    {

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)

        {

            AudioListener listener = listeners[i];

            if (listener != null && listener.enabled)

                return true;

        }



        return false;

    }



    public static void EnforceSingleListener()

    {

        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid())

            return;



        AudioListener primary = ResolveOrCreatePrimaryListener(activeScene);

        if (primary == null)

            return;



        if (!primary.gameObject.activeInHierarchy)

            primary.gameObject.SetActive(true);



        primary.enabled = true;



        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < listeners.Length; i++)

        {

            AudioListener listener = listeners[i];

            if (listener == null || listener == primary)

                continue;



            listener.enabled = false;

        }

    }



    private static AudioListener ResolveOrCreatePrimaryListener(Scene activeScene)

    {

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (listeners == null || listeners.Length == 0)

            return CreateListenerOnSceneMainCamera(activeScene);



        for (int i = 0; i < listeners.Length; i++)

        {

            AudioListener listener = listeners[i];

            if (listener == null || listener.gameObject.scene != activeScene)

                continue;



            if (string.Equals(listener.gameObject.name, "Main Camera", System.StringComparison.Ordinal))

                return listener;

        }



        Camera mainCam = Camera.main;

        if (mainCam != null && mainCam.gameObject.scene == activeScene)

        {

            AudioListener onMain = mainCam.GetComponent<AudioListener>();

            if (onMain != null)

                return onMain;

        }



        for (int i = 0; i < listeners.Length; i++)

        {

            AudioListener listener = listeners[i];

            if (listener != null && listener.gameObject.scene == activeScene)

                return listener;

        }



        return CreateListenerOnSceneMainCamera(activeScene);

    }



    private static AudioListener CreateListenerOnSceneMainCamera(Scene activeScene)

    {

        GameObject[] roots = activeScene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)

        {

            Transform mainCam = FindMainCameraTransform(roots[i].transform);

            if (mainCam == null)

                continue;



            if (!mainCam.gameObject.activeInHierarchy)

                mainCam.gameObject.SetActive(true);



            AudioListener listener = mainCam.GetComponent<AudioListener>();

            if (listener == null)

                listener = mainCam.gameObject.AddComponent<AudioListener>();



            listener.enabled = true;

            return listener;

        }



        return null;

    }



    private static Transform FindMainCameraTransform(Transform root)

    {

        if (root == null)

            return null;



        if (string.Equals(root.name, "Main Camera", System.StringComparison.Ordinal) &&

            root.GetComponent<Camera>() != null)

            return root;



        for (int i = 0; i < root.childCount; i++)

        {

            Transform found = FindMainCameraTransform(root.GetChild(i));

            if (found != null)

                return found;

        }



        return null;

    }

}


