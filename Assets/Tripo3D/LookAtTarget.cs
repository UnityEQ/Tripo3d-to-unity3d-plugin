using UnityEngine;

[ExecuteAlways]
public sealed class LookAtTarget : MonoBehaviour
{
    public Transform target;
    public Vector3 worldOffset = new Vector3(0f, 0.75f, 0f);
    public bool useRendererBounds = true;

    void LateUpdate()
    {
        if (target == null)
            return;
        var aim = AimPoint();
        var dir = aim - transform.position;
        if (dir.sqrMagnitude < 1e-8f)
            return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    Vector3 AimPoint()
    {
        if (useRendererBounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds.center + Vector3.up * (bounds.extents.y * 0.12f);
            }
        }

        return target.position + worldOffset;
    }
}
