using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class LookAtTarget : MonoBehaviour
{
    const float MinPitch = -25f;
    const float MaxPitch = 70f;
    const float MinRadius = 0.35f;

    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 0.75f, 0f);
    public bool useRendererBounds = true;

    [Header("Camera orbit")]
    [Range(-180f, 180f)]
    [InspectorName("Left / right")]
    [Tooltip("Orbit the camera left and right around the look target.")]
    public float orbitYaw;

    [Range(-25f, 70f)]
    [InspectorName("Look up / down")]
    [Tooltip("Raise or lower the camera around the look target. Higher looks down from above.")]
    public float orbitPitch;

    [Range(-35f, 25f)]
    [InspectorName("Aim tilt")]
    [Tooltip("Extra aim tilt in degrees after orbit. Negative looks up, positive looks down.")]
    public float extraPitch = -10f;

    [SerializeField, HideInInspector] bool orbitInitialized;
    [SerializeField, HideInInspector] float orbitRadius = 2f;

    // Refill on each query so hierarchy changes and animated bounds stay live.
    readonly List<Renderer> renderers = new List<Renderer>();

    float appliedYaw = float.NaN;
    float appliedPitch = float.NaN;
    Vector3 appliedPos;

    void OnEnable()
    {
        if (!orbitInitialized)
            ReadOrbitFromPose();
        ApplyLook();
    }

    void OnValidate()
    {
        orbitPitch = Mathf.Clamp(orbitPitch, MinPitch, MaxPitch);
        if (target == null)
            return;
        ApplyLook();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (!orbitInitialized || orbitRadius < MinRadius)
            ReadOrbitFromPose();
        else if (!Mathf.Approximately(orbitYaw, appliedYaw) || !Mathf.Approximately(orbitPitch, appliedPitch))
            ApplyOrbit();
        else if ((transform.position - appliedPos).sqrMagnitude > 0.0001f)
            ReadOrbitFromPose();

        ApplyLook();
    }

    public Vector3 Pivot()
    {
        return AimPoint();
    }

    public void ReadOrbitFromPose()
    {
        if (target == null)
            return;
        var offset = transform.position - Pivot();
        orbitRadius = Mathf.Max(MinRadius, offset.magnitude);
        var xz = new Vector2(offset.x, offset.z).magnitude;
        orbitYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        orbitPitch = Mathf.Clamp(Mathf.Atan2(offset.y, Mathf.Max(0.0001f, xz)) * Mathf.Rad2Deg, MinPitch, MaxPitch);
        appliedYaw = orbitYaw;
        appliedPitch = orbitPitch;
        appliedPos = transform.position;
        orbitInitialized = true;
    }

    public void ApplyOrbit()
    {
        if (target == null)
            return;
        orbitRadius = Mathf.Max(MinRadius, orbitRadius);
        orbitPitch = Mathf.Clamp(orbitPitch, MinPitch, MaxPitch);
        var yaw = orbitYaw * Mathf.Deg2Rad;
        var pitch = orbitPitch * Mathf.Deg2Rad;
        var horizontal = Mathf.Cos(pitch) * orbitRadius;
        transform.position = Pivot() + new Vector3(
            Mathf.Sin(yaw) * horizontal,
            Mathf.Sin(pitch) * orbitRadius,
            Mathf.Cos(yaw) * horizontal);
        appliedYaw = orbitYaw;
        appliedPitch = orbitPitch;
        appliedPos = transform.position;
        orbitInitialized = true;
    }

    public void ApplyLook()
    {
        if (target == null)
            return;
        var aim = AimPoint();
        var dir = aim - transform.position;
        if (dir.sqrMagnitude < 1e-8f)
            return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(extraPitch, 0f, 0f);
    }

    Vector3 AimPoint()
    {
        if (useRendererBounds)
        {
            target.GetComponentsInChildren(false, renderers);
            if (renderers.Count > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Count; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds.center + Vector3.up * (bounds.extents.y * 0.12f);
            }
        }

        return target.position + worldOffset;
    }
}
