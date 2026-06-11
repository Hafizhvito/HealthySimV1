using UnityEngine;



/// <summary>

/// Attach to Main Camera in each gameplay/menu scene that needs audio.

/// Designers can see and toggle this in the Inspector; prevents missing AudioListener warnings.

/// </summary>

[DisallowMultipleComponent]

[RequireComponent(typeof(Camera))]

public class SceneAudioListenerGuard : MonoBehaviour

{

    void Awake()

    {

        EnsureListener();

    }



    void OnEnable()

    {

        EnsureListener();

    }



    void EnsureListener()

    {

        AudioListener listener = GetComponent<AudioListener>();

        if (listener == null)

            listener = gameObject.AddComponent<AudioListener>();



        listener.enabled = true;

    }

}


