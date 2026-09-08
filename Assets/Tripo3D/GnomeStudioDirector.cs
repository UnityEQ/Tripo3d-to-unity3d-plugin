using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GnomeStudioDirector : MonoBehaviour
{
    public enum PickMode
    {
        Manual,
        ClosestToFollow
    }

    [Tooltip("Marker cameras look at. Move this to aim the rig.")]
    public Transform follow;
    [Tooltip("Character the follow marker sits on.")]
    public Transform subject;
    public PickMode pickMode = PickMode.Manual;
    [Tooltip("Which camera is live when Pick Mode is Manual.")]
    public int activeCamera;
    public List<Camera> cameras = new List<Camera>();
    [Tooltip("Every live camera aims at CameraFollow.")]
    public bool lookAtFollow = true;
    [Range(0.05f, 2f)]
    [Tooltip("How much closer another camera must be before it takes over.")]
    public float closestHysteresis = 0.28f;
    [Tooltip("Keep depth of field focused on CameraFollow.")]
    public bool rackFocus = true;
    [Tooltip("When a Timeline cinematic is playing, skip manual/closest switching.")]
    public bool cinematicActive;

    Camera live;
    DepthOfField dof;
    float lastFocus = -1f;

    public Camera LiveCamera => live;

    void OnEnable()
    {
        CacheDof();
        Apply();
    }

    void OnValidate()
    {
        activeCamera = Mathf.Max(0, activeCamera);
        Apply();
    }

    void LateUpdate()
    {
        if (cinematicActive)
        {
            UpdateFocus();
            return;
        }

        if (pickMode == PickMode.ClosestToFollow)
            PickClosest();
        AimLive();
        UpdateFocus();
    }

    public void Apply()
    {
        if (cinematicActive)
            return;
        if (cameras == null || cameras.Count == 0)
            CollectCameras();
        if (pickMode == PickMode.ClosestToFollow)
            PickClosest();
        else
            SetLiveIndex(activeCamera);
        AimLive();
        UpdateFocus();
    }

    public void SetLiveIndex(int index)
    {
        if (cameras == null || cameras.Count == 0)
            return;
        index = Mathf.Clamp(index, 0, cameras.Count - 1);
        activeCamera = index;
        SetLive(cameras[index]);
    }

    public void SetLive(Camera cam)
    {
        if (cam == null)
            return;
        for (var i = 0; i < cameras.Count; i++)
        {
            var c = cameras[i];
            if (c == null)
                continue;
            var on = c == cam;
            if (c.enabled != on)
                c.enabled = on;
            var listener = c.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = on;
            if (on)
            {
                if (!c.gameObject.activeSelf)
                    c.gameObject.SetActive(true);
                if (!c.CompareTag("MainCamera"))
                    c.tag = "MainCamera";
                live = c;
                activeCamera = i;
            }
            else if (c.CompareTag("MainCamera"))
                c.tag = "Untagged";
        }
    }

    public void ActivateCameraObjects()
    {
        if (cameras == null)
            return;
        for (var i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            if (cam != null && !cam.gameObject.activeSelf)
                cam.gameObject.SetActive(true);
        }
    }

    public void CollectCameras()
    {
        if (cameras == null)
            cameras = new List<Camera>();
        cameras.Clear();
        var all = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (var i = 0; i < all.Length; i++)
        {
            var cam = all[i];
            if (cam == null || cam.targetTexture != null)
                continue;
            if (cam.hideFlags != HideFlags.None || !cam.gameObject.scene.IsValid())
                continue;
            if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                continue;
            cameras.Add(cam);
        }

        cameras.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    void PickClosest()
    {
        if (follow == null || cameras == null || cameras.Count == 0)
            return;
        var pivot = follow.position;
        var best = live;
        var bestDist = live != null ? HorizontalDistance(live.transform.position, pivot) : float.MaxValue;
        for (var i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            if (cam == null)
                continue;
            var dist = HorizontalDistance(cam.transform.position, pivot);
            var margin = cam == live ? 0f : closestHysteresis;
            if (dist + margin < bestDist)
            {
                best = cam;
                bestDist = dist;
            }
        }

        if (best != null && best != live)
            SetLive(best);
        else if (live == null && best != null)
            SetLive(best);
    }

    void AimLive()
    {
        if (!lookAtFollow || follow == null || cameras == null)
            return;
        for (var i = 0; i < cameras.Count; i++)
        {
            var cam = cameras[i];
            if (cam == null)
                continue;
            var look = cam.GetComponent<LookAtTarget>();
            if (look == null)
                look = cam.gameObject.AddComponent<LookAtTarget>();
            look.target = follow;
            look.useRendererBounds = false;
            look.worldOffset = Vector3.zero;
            if (cam.enabled)
                look.ApplyLook();
        }
    }

    void CacheDof()
    {
        var volume = FindFirstObjectByType<Volume>();
        if (volume == null || volume.sharedProfile == null)
            return;
        volume.sharedProfile.TryGet(out dof);
    }

    void UpdateFocus()
    {
        if (!rackFocus || live == null || follow == null)
            return;
        if (dof == null)
            CacheDof();
        if (dof == null)
            return;
        var dist = Vector3.Distance(live.transform.position, follow.position);
        if (Mathf.Abs(dist - lastFocus) < 0.01f)
            return;
        lastFocus = dist;
        dof.focusDistance.Override(Mathf.Clamp(dist, 0.25f, 12f));
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y;
        return Vector3.Distance(a, b);
    }

    void OnDrawGizmosSelected()
    {
        if (follow == null)
            return;
        Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.95f);
        Gizmos.DrawWireSphere(follow.position, 0.07f);
        if (live != null)
        {
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.85f);
            Gizmos.DrawLine(live.transform.position, follow.position);
        }
    }
}
