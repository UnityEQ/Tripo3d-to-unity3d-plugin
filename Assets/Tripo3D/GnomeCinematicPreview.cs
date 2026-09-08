using UnityEngine;

[DisallowMultipleComponent]
public sealed class GnomeCinematicPreview : MonoBehaviour
{
    public bool playOnStart = true;

    GnomeStudioDirector studio;

    void Start()
    {
        studio = GetComponent<GnomeStudioDirector>();
        if (playOnStart && studio != null)
            studio.StartOrbit();
    }

    [ContextMenu("Play Preview")]
    public void PlayPreview()
    {
        studio = GetComponent<GnomeStudioDirector>();
        if (studio != null)
            studio.StartOrbit();
    }

    [ContextMenu("Stop Preview")]
    public void StopPreview()
    {
        studio = GetComponent<GnomeStudioDirector>();
        if (studio != null)
            studio.StopOrbit();
    }
}
