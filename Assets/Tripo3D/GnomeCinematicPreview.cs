using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GnomeCinematicPreview : MonoBehaviour
{
    [Tooltip("The gnome (Animator lives on this object or a child).")]
    public Transform subject;
    public bool playOnStart = true;
    public bool loop = true;

    CinemachineBrain brain;
    Animator animator;
    LookAtTarget lookAt;
    Shot wide, portrait, runArc, hero;
    Shot live;
    float yawSpeed;
    float verticalGoal;
    float radialGoal;
    bool playing;

    struct Shot
    {
        public CinemachineCamera Camera;
        public CinemachineOrbitalFollow Orbit;
    }

    void Start()
    {
        if (playOnStart)
            PlayPreview();
    }

    [ContextMenu("Play Preview")]
    public void PlayPreview()
    {
        StopAllCoroutines();
        StartCoroutine(RunPreview());
    }

    IEnumerator RunPreview()
    {
        if (!EnsureRig())
            yield break;

        playing = true;
        if (lookAt != null)
            lookAt.enabled = false;

        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        SetLive(wide, 14f, 16f, 1f);
        yield return null;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.45f);

        do
        {
            var idle = ClipLength("Idle", 2f);
            var run = ClipLength("Run", 1f);
            var jump = ClipLength("Jump", 1.2f);
            var slash = ClipLength("SwordSlash", 1f);

            PlayState("Idle");
            SetLive(wide, 16f, 15f, 1.05f);
            yield return Wait(idle * 2f);

            SetLive(portrait, 10f, 18f, 0.72f);
            yield return Wait(idle * 1.5f);

            PlayState("Run");
            SetLive(runArc, 38f, 9f, 0.9f);
            yield return Wait(Mathf.Max(run * 3f, 3f));

            PlayState("Jump");
            SetLive(hero, 8f, 5f, 0.82f);
            yield return Wait(jump + 0.45f);

            PlayState("SwordSlash");
            SetLive(portrait, 22f, 12f, 0.78f);
            yield return Wait(slash + 0.55f);

            PlayState("Idle");
            SetLive(wide, 18f, 14f, 1.08f);
            yield return Wait(idle * 1.5f);
        }
        while (loop);

        playing = false;
        if (lookAt != null)
            lookAt.enabled = true;
    }

    void LateUpdate()
    {
        if (!playing || live.Orbit == null)
            return;
        live.Orbit.HorizontalAxis.Value += yawSpeed * Time.deltaTime;
        live.Orbit.VerticalAxis.Value = Mathf.Lerp(live.Orbit.VerticalAxis.Value, verticalGoal, Time.deltaTime * 1.6f);
        live.Orbit.RadialAxis.Value = Mathf.Lerp(live.Orbit.RadialAxis.Value, radialGoal, Time.deltaTime * 1.2f);
    }

    bool EnsureRig()
    {
        if (subject == null)
        {
            var gnome = GameObject.Find("Gnome") ?? GameObject.Find("Character");
            if (gnome != null)
                subject = gnome.transform;
        }

        if (subject == null)
            return false;

        animator = subject.GetComponentInChildren<Animator>();
        var cam = Camera.main;
        if (cam == null || animator == null)
            return false;

        lookAt = cam.GetComponent<LookAtTarget>();
        brain = cam.GetComponent<CinemachineBrain>();
        if (brain == null)
            brain = cam.gameObject.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 1.45f);

        var height = SubjectHeight();
        var look = new Vector3(0f, height * 0.55f, 0f);
        wide = GetOrCreateShot("CM Wide", height * 2.15f, 16f, 40f, look, 0f);
        portrait = GetOrCreateShot("CM Portrait", height * 1.28f, 18f, 28f, look, 35f);
        runArc = GetOrCreateShot("CM Run", height * 1.7f, 8f, 34f, look, 125f);
        hero = GetOrCreateShot("CM Hero", height * 1.5f, 4f, 32f, look, 210f);
        return true;
    }

    Shot GetOrCreateShot(string name, float radius, float pitch, float fov, Vector3 lookOffset, float startYaw)
    {
        var existing = transform.Find(name);
        GameObject go;
        if (existing != null)
            go = existing.gameObject;
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(transform, false);
        }

        var vcam = go.GetComponent<CinemachineCamera>();
        if (vcam == null)
            vcam = go.AddComponent<CinemachineCamera>();
        vcam.Target.TrackingTarget = subject;
        vcam.Target.CustomLookAtTarget = false;
        vcam.Lens = LensSettings.Default;
        vcam.Lens.FieldOfView = fov;
        vcam.Lens.NearClipPlane = 0.05f;
        vcam.Priority = 0;

        var orbit = go.GetComponent<CinemachineOrbitalFollow>();
        if (orbit == null)
            orbit = go.AddComponent<CinemachineOrbitalFollow>();
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = Mathf.Max(0.6f, radius);
        orbit.TargetOffset = lookOffset;
        orbit.HorizontalAxis.Wrap = true;
        orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbit.HorizontalAxis.Recentering.Enabled = false;
        orbit.HorizontalAxis.Value = startYaw;
        orbit.VerticalAxis.Range = new Vector2(-8f, 40f);
        orbit.VerticalAxis.Wrap = false;
        orbit.VerticalAxis.Recentering.Enabled = false;
        orbit.VerticalAxis.Value = pitch;
        orbit.RadialAxis.Range = new Vector2(0.55f, 1.35f);
        orbit.RadialAxis.Recentering.Enabled = false;
        orbit.RadialAxis.Value = 1f;

        var aim = go.GetComponent<CinemachineHardLookAt>();
        if (aim == null)
            aim = go.AddComponent<CinemachineHardLookAt>();
        aim.LookAtOffset = lookOffset;

        return new Shot { Camera = vcam, Orbit = orbit };
    }

    void SetLive(Shot shot, float yaw, float pitch, float radial)
    {
        wide.Camera.Priority = 0;
        portrait.Camera.Priority = 0;
        runArc.Camera.Priority = 0;
        hero.Camera.Priority = 0;
        shot.Camera.Priority = 100;
        live = shot;
        yawSpeed = yaw;
        verticalGoal = pitch;
        radialGoal = radial;
    }

    void PlayState(string state)
    {
        if (animator == null)
            return;
        if (HasState(state))
            animator.CrossFadeInFixedTime(state, 0.22f, 0, 0f);
        else
            animator.Play(state, 0, 0f);
    }

    bool HasState(string state)
    {
        return animator.HasState(0, Animator.StringToHash(state));
    }

    float ClipLength(string name, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return fallback;
        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null)
            return fallback;
        for (var i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (clip == null)
                continue;
            if (clip.name == name || clip.name.EndsWith("|" + name) || clip.name.EndsWith("_" + name))
                return Mathf.Max(0.2f, clip.length);
        }

        return fallback;
    }

    float SubjectHeight()
    {
        var renderers = subject.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return 1f;
        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return Mathf.Max(0.7f, bounds.size.y);
    }

    static IEnumerator Wait(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
    }
}
