using UnityEngine;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class GnomeCinematicPreview : MonoBehaviour
{
    [Tooltip("The character the shots are built around.")]
    public Transform subject;
    [Tooltip("Look-at marker driven by the Timeline animation track.")]
    public Transform follow;
    public PlayableDirector playable;
    public bool playOnStart = true;
    public bool loop = true;

    GnomeStudioDirector studio;
    bool cinematic;

    void Start()
    {
        if (playOnStart)
            PlayPreview();
    }

    void OnEnable()
    {
        if (playable != null)
        {
            playable.stopped += OnPlayableStopped;
            playable.played += OnPlayablePlayed;
        }
    }

    void OnDisable()
    {
        if (playable != null)
        {
            playable.stopped -= OnPlayableStopped;
            playable.played -= OnPlayablePlayed;
        }

        if (cinematic)
            ExitCinematicMode();
    }

    void LateUpdate()
    {
        if (cinematic)
            SyncLiveCamera();
    }

    [ContextMenu("Play Preview")]
    public void PlayPreview()
    {
        if (playable == null)
            playable = GetComponent<PlayableDirector>();
        if (playable == null || playable.playableAsset == null)
        {
            Debug.LogWarning("[Gnome Studio] No Timeline on Playable Director. Use Tripo 3D > Refresh Gnome Studio Look.");
            return;
        }

        EnterCinematicMode();
        playable.time = 0;
        playable.extrapolationMode = loop ? DirectorWrapMode.Loop : DirectorWrapMode.Hold;
        playable.Play();
    }

    [ContextMenu("Stop Preview")]
    public void StopPreview()
    {
        if (playable != null)
            playable.Stop();
        ExitCinematicMode();
    }

    void OnPlayablePlayed(PlayableDirector _)
    {
        EnterCinematicMode();
    }

    void OnPlayableStopped(PlayableDirector _)
    {
        if (!loop)
            ExitCinematicMode();
    }

    public void EnterCinematicMode()
    {
        cinematic = true;
        studio = GetComponent<GnomeStudioDirector>();
        if (studio != null)
            studio.cinematicActive = true;
        SyncLiveCamera();
    }

    public void ExitCinematicMode()
    {
        cinematic = false;
        studio = GetComponent<GnomeStudioDirector>();
        if (studio != null)
        {
            studio.cinematicActive = false;
            studio.ActivateCameraObjects();
            studio.Apply();
        }
    }

    void SyncLiveCamera()
    {
        if (studio == null || studio.cameras == null)
            return;
        Camera found = null;
        for (var i = 0; i < studio.cameras.Count; i++)
        {
            var cam = studio.cameras[i];
            if (cam == null || !cam.gameObject.activeInHierarchy)
                continue;
            found = cam;
            break;
        }

        if (found == null)
            return;
        for (var i = 0; i < studio.cameras.Count; i++)
        {
            var cam = studio.cameras[i];
            if (cam == null)
                continue;
            var on = cam == found;
            if (cam.enabled != on)
                cam.enabled = on;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = on;
            if (on)
            {
                if (!cam.CompareTag("MainCamera"))
                    cam.tag = "MainCamera";
                var look = cam.GetComponent<LookAtTarget>();
                if (look != null)
                    look.ApplyLook();
            }
            else if (cam.CompareTag("MainCamera"))
                cam.tag = "Untagged";
        }
    }
}
