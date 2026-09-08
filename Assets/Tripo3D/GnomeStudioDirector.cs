using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GnomeStudioDirector : MonoBehaviour
{
    public enum Spot
    {
        Front,
        Right,
        Back,
        Left
    }

    const float MinDistance = 1.87f;
    const float MaxDistance = 6.3f;
    const float MinHeight = 0.05f;
    const float MaxHeight = 4.5f;

    [Tooltip("The one studio camera. Extra shot cameras are not used.")]
    public Camera studioCamera;
    [Tooltip("Look-at marker. The camera always aims here.")]
    public Transform follow;
    [Tooltip("Character the follow marker sits on.")]
    public Transform subject;
    [Tooltip("Front / Right / Back / Left around the subject.")]
    public Spot spot = Spot.Front;
    [Range(0f, 360f)]
    [InspectorName("Orbit")]
    [Tooltip("360° horizontal orbit around the character. 0 is Front, 90 Right, 180 Back, 270 Left.")]
    public float orbit;
    [Range(1.87f, 6.3f)]
    [Tooltip("Horizontal distance from the look target. Lower is closer.")]
    public float distance = 2.6f;
    [Range(0.05f, 4.5f)]
    [InspectorName("Height (Y)")]
    [Tooltip("World Y of the camera transform.")]
    public float height = 1.4f;
    [Tooltip("Keep depth of field focused on the look target.")]
    public bool rackFocus = true;
    [Tooltip("The environmental sun / moon Directional Light.")]
    public Light environmentLight;
    [Range(0f, 1f)]
    [InspectorName("Daylight")]
    [Tooltip("Environmental look. 0 is night, 1 is day.")]
    public float daylight = 1f;
    [Tooltip("Spin the camera around the character. Distance, height, and orbit stay live.")]
    public bool orbiting;
    [Range(-90f, 90f)]
    [Tooltip("Degrees per second while orbiting. Negative reverses.")]
    public float orbitSpeed = 46f;
    [Tooltip("Start orbiting when Play Mode begins.")]
    public bool orbitOnPlay = true;

    DepthOfField dof;
    ColorAdjustments colorAdjust;
    WhiteBalance whiteBalance;
    SplitToning splitToning;
    Bloom bloom;
    Vignette vignette;
    float lastFocus = -1f;
    float lastDaylight = float.NaN;
#if UNITY_EDITOR
    double lastEditorTime;
#endif

    public Camera LiveCamera => studioCamera != null ? studioCamera : Camera.main;
    public Transform LookTarget => follow != null ? follow : subject;
    public bool cinematicActive
    {
        get => orbiting;
        set => orbiting = value;
    }

    public float YawOf(Spot at)
    {
        return SubjectHeading() + OrbitOf(at);
    }

    public static float OrbitOf(Spot at)
    {
        switch (at)
        {
            case Spot.Right: return 90f;
            case Spot.Back: return 180f;
            case Spot.Left: return 270f;
            default: return 0f;
        }
    }

    void OnEnable()
    {
        CacheDof();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
        UnityEditor.EditorApplication.update += EditorTick;
#endif
        if (!orbiting)
            Apply();
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= EditorTick;
#endif
    }

    void Start()
    {
        if (orbitOnPlay && Application.isPlaying)
            StartOrbit();
    }

    void OnValidate()
    {
        orbit = Mathf.Repeat(orbit, 360f);
        distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        height = Mathf.Clamp(height, MinHeight, MaxHeight);
        orbitSpeed = Mathf.Clamp(orbitSpeed, -90f, 90f);
        daylight = Mathf.Clamp01(daylight);
        lastDaylight = float.NaN;
        SyncSpotFromOrbit();
        Apply();
    }

    void LateUpdate()
    {
        if (Application.isPlaying)
            TickOrbit();
        else if (!orbiting)
            Apply();
    }

    public void Apply()
    {
        orbit = Mathf.Repeat(orbit, 360f);
        PlaceAndLook(SubjectHeading() + orbit);
        UpdateFocus();
        ApplyEnvironment();
    }

    public void SetDaylight(float value)
    {
        daylight = Mathf.Clamp01(value);
        lastDaylight = float.NaN;
        ApplyEnvironment();
    }

    public void StartOrbit()
    {
        ResetOrbitPose();
        orbiting = true;
        Apply();
#if UNITY_EDITOR
        lastEditorTime = 0;
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void StopOrbit()
    {
        orbiting = false;
        ResetOrbitPose();
        Apply();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    void ResetOrbitPose()
    {
        orbit = 0f;
        distance = MinDistance;
        height = MinHeight;
        daylight = 0f;
        lastDaylight = float.NaN;
        SyncSpotFromOrbit();
    }

    public void SetSpot(Spot next)
    {
        spot = next;
        orbit = OrbitOf(next);
        Apply();
    }

    public void SetOrbit(float value)
    {
        orbit = Mathf.Repeat(value, 360f);
        SyncSpotFromOrbit();
        Apply();
    }

    public void SetDistance(float value)
    {
        distance = Mathf.Clamp(value, MinDistance, MaxDistance);
        Apply();
    }

    public void SetHeight(float value)
    {
        height = Mathf.Clamp(value, MinHeight, MaxHeight);
        Apply();
    }

    public Vector3 SpotPosition(Spot at)
    {
        return PolarPosition(YawOf(at));
    }

    void PlaceAndLook(float yaw)
    {
        var cam = LiveCamera;
        if (cam == null)
            return;
        if (studioCamera == null)
            studioCamera = cam;
        if (!cam.gameObject.activeSelf)
            cam.gameObject.SetActive(true);
        if (!cam.enabled)
            cam.enabled = true;
        if (!cam.CompareTag("MainCamera"))
            cam.tag = "MainCamera";
        var listener = cam.GetComponent<AudioListener>();
        if (listener != null && !listener.enabled)
            listener.enabled = true;

        cam.transform.position = PolarPosition(yaw);
        var aim = LookTarget;
        if (aim == null)
            return;
        var dir = aim.position - cam.transform.position;
        if (dir.sqrMagnitude < 1e-8f)
            return;
        cam.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Vector3 PolarPosition(float yaw)
    {
        var pivot = LookTarget != null ? LookTarget.position : Vector3.up;
        var rad = yaw * Mathf.Deg2Rad;
        var radius = Mathf.Clamp(distance, MinDistance, MaxDistance);
        return new Vector3(
            pivot.x + Mathf.Sin(rad) * radius,
            height,
            pivot.z + Mathf.Cos(rad) * radius);
    }

    void CycleOrbitSliders()
    {
        var wave = 0.5f - 0.5f * Mathf.Cos(Mathf.Repeat(orbit, 360f) * Mathf.Deg2Rad);
        distance = Mathf.Lerp(MinDistance, MaxDistance, wave);
        height = Mathf.Lerp(MinHeight, MaxHeight, wave);
        daylight = wave;
    }

    float DeltaTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var now = UnityEditor.EditorApplication.timeSinceStartup;
            var dt = lastEditorTime > 0.0 ? (float)(now - lastEditorTime) : 0.016f;
            lastEditorTime = now;
            return Mathf.Clamp(dt, 0f, 0.05f);
        }
#endif
        return Time.deltaTime;
    }

    void TickOrbit()
    {
        if (orbiting)
        {
            orbit = Mathf.Repeat(orbit + orbitSpeed * DeltaTime(), 360f);
            CycleOrbitSliders();
            SyncSpotFromOrbit();
        }

        Apply();
    }

#if UNITY_EDITOR
    void EditorTick()
    {
        if (Application.isPlaying || !isActiveAndEnabled || !orbiting)
            return;
        TickOrbit();
    }
#endif

    public void SyncSpotFromOrbit()
    {
        var wrapped = Mathf.Repeat(orbit, 360f);
        if (wrapped >= 315f || wrapped < 45f)
            spot = Spot.Front;
        else if (wrapped < 135f)
            spot = Spot.Right;
        else if (wrapped < 225f)
            spot = Spot.Back;
        else
            spot = Spot.Left;
    }

    float SubjectHeading()
    {
        var t = subject != null ? subject : LookTarget;
        if (t != null)
        {
            var forward = t.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.05f)
                return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        if (t == null)
            return 180f;
        var renderers = t.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return 180f;
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds.size.x <= bounds.size.z ? -90f : 180f;
    }

    void CacheDof()
    {
        var volume = FindFirstObjectByType<Volume>();
        if (volume == null || volume.sharedProfile == null)
            return;
        var profile = volume.sharedProfile;
        profile.TryGet(out dof);
        profile.TryGet(out colorAdjust);
        profile.TryGet(out whiteBalance);
        profile.TryGet(out splitToning);
        profile.TryGet(out bloom);
        profile.TryGet(out vignette);
    }

    public void ApplyEnvironment()
    {
        var t = Mathf.Clamp01(daylight);
        if (!float.IsNaN(lastDaylight) && Mathf.Abs(t - lastDaylight) < 0.0005f)
            return;
        lastDaylight = t;
        t = t * t * (3f - 2f * t);

        var sun = FindEnvironmentLight();
        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(0.18f, 1.55f, t);
            sun.color = Color.Lerp(new Color(0.48f, 0.62f, 1f), new Color(1f, 0.96f, 0.9f), t);
            sun.shadowStrength = Mathf.Lerp(0.55f, 0.85f, t);
            if (sun.useColorTemperature)
                sun.colorTemperature = Mathf.Lerp(8800f, 5200f, t);
            sun.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(16f, -40f, 0f),
                Quaternion.Euler(48f, -28f, 0f),
                t);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(new Color(0.05f, 0.07f, 0.14f), new Color(0.42f, 0.44f, 0.48f), t);
        RenderSettings.ambientIntensity = Mathf.Lerp(0.28f, 1f, t);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.28f, 1f, t);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(new Color(0.03f, 0.04f, 0.09f), new Color(0.62f, 0.64f, 0.66f), t);
        RenderSettings.fogDensity = Mathf.Lerp(0.012f, 0.0035f, t);

        if (colorAdjust == null)
            CacheDof();
        if (colorAdjust != null)
        {
            colorAdjust.postExposure.Override(Mathf.Lerp(-1.15f, 0.2f, t));
            colorAdjust.contrast.Override(Mathf.Lerp(10f, 18f, t));
            colorAdjust.saturation.Override(Mathf.Lerp(-6f, 8f, t));
            colorAdjust.colorFilter.Override(Color.Lerp(new Color(0.58f, 0.7f, 1f), new Color(1f, 0.97f, 0.93f), t));
        }

        if (whiteBalance != null)
        {
            whiteBalance.temperature.Override(Mathf.Lerp(-28f, 10f, t));
            whiteBalance.tint.Override(Mathf.Lerp(6f, -2f, t));
        }

        if (splitToning != null)
        {
            splitToning.shadows.Override(Color.Lerp(new Color(0.12f, 0.2f, 0.48f), new Color(0.28f, 0.38f, 0.52f), t));
            splitToning.highlights.Override(Color.Lerp(new Color(0.45f, 0.55f, 0.85f), new Color(1f, 0.84f, 0.68f), t));
        }

        if (bloom != null)
        {
            bloom.threshold.Override(Mathf.Lerp(0.55f, 0.78f, t));
            bloom.intensity.Override(Mathf.Lerp(0.55f, 0.34f, t));
            bloom.tint.Override(Color.Lerp(new Color(0.65f, 0.78f, 1f), new Color(1f, 0.95f, 0.88f), t));
        }

        if (vignette != null)
        {
            vignette.intensity.Override(Mathf.Lerp(0.38f, 0.22f, t));
            vignette.color.Override(Color.Lerp(new Color(0.02f, 0.03f, 0.08f), new Color(0.04f, 0.025f, 0.02f), t));
        }

        SetLightIntensity("Fill Light", Mathf.Lerp(0.9f, 3.2f, t));
        SetLightIntensity("Soft Overhead", Mathf.Lerp(0.55f, 1.55f, t));
        SetLightIntensity("Rim Light", Mathf.Lerp(2.2f, 6.5f, t));
        SetLightIntensity("Deco Uplight 1", Mathf.Lerp(2.8f, 1.55f, t));
        SetLightIntensity("Deco Uplight 2", Mathf.Lerp(2.8f, 1.55f, t));
        SetLightIntensity("Deco Uplight 3", Mathf.Lerp(2.8f, 1.55f, t));
        SetLightIntensity("Deco Uplight 4", Mathf.Lerp(2.8f, 1.55f, t));
    }

    Light FindEnvironmentLight()
    {
        if (environmentLight != null)
            return environmentLight;
        var named = GameObject.Find("Directional Light");
        if (named != null)
            environmentLight = named.GetComponent<Light>();
        if (environmentLight == null)
            environmentLight = RenderSettings.sun;
        if (environmentLight == null)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    environmentLight = lights[i];
                    break;
                }
            }
        }

        return environmentLight;
    }

    static void SetLightIntensity(string name, float intensity)
    {
        var go = GameObject.Find(name);
        if (go == null)
            return;
        var light = go.GetComponent<Light>();
        if (light != null)
            light.intensity = intensity;
    }

    void UpdateFocus()
    {
        var cam = LiveCamera;
        var aim = LookTarget;
        if (!rackFocus || cam == null || aim == null)
            return;
        if (dof == null)
            CacheDof();
        if (dof == null)
            return;
        var dist = Vector3.Distance(cam.transform.position, aim.position);
        if (Mathf.Abs(dist - lastFocus) < 0.01f)
            return;
        lastFocus = dist;
        dof.focusDistance.Override(Mathf.Clamp(dist, 0.25f, 12f));
    }

    void OnDrawGizmosSelected()
    {
        var aim = LookTarget;
        if (aim == null)
            return;
        Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.95f);
        Gizmos.DrawWireSphere(aim.position, 0.07f);
        var spots = new[] { Spot.Front, Spot.Right, Spot.Back, Spot.Left };
        for (var i = 0; i < spots.Length; i++)
        {
            var pos = SpotPosition(spots[i]);
            var live = spots[i] == spot && Mathf.Abs(Mathf.DeltaAngle(orbit, OrbitOf(spots[i]))) < 1f;
            Gizmos.color = live ? new Color(0.4f, 0.85f, 1f, 0.95f) : new Color(1f, 0.82f, 0.2f, 0.45f);
            Gizmos.DrawWireSphere(pos, live ? 0.09f : 0.06f);
            Gizmos.DrawLine(aim.position, pos);
        }

        var cam = LiveCamera;
        if (cam != null)
        {
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.85f);
            Gizmos.DrawLine(cam.transform.position, aim.position);
        }
    }
}
