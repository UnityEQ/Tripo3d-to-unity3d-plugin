using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GnomeCharacterFill : MonoBehaviour
{
    [Range(0f, 2f)]
    [Tooltip("Only the gnome fill lights. 1 is the original look, 0.7 is 30% dimmer. Walls and key light stay put.")]
    public float brightness = 0.029f;

    public Light bodyFill;
    public Light eyeLight;
    public Light catchlight;

    [HideInInspector] public float bodyBase = 2.15f;
    [HideInInspector] public float eyeBase = 1.35f;
    [HideInInspector] public float catchBase = 0.85f;

    float applied = -1f;

    void OnEnable()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    void Update()
    {
        if (Mathf.Abs(applied - brightness) > 0.0001f)
            Apply();
    }

    public void Apply()
    {
        applied = brightness;
        var scale = Mathf.Max(0f, brightness);
        if (bodyFill != null)
            bodyFill.intensity = bodyBase * scale;
        if (eyeLight != null)
            eyeLight.intensity = eyeBase * scale;
        if (catchlight != null)
            catchlight.intensity = catchBase * scale;
    }
}
