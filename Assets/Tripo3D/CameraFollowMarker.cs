using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CameraFollowMarker : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.82f, 0.18f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, 0.06f);
        Gizmos.DrawLine(transform.position + Vector3.left * 0.08f, transform.position + Vector3.right * 0.08f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.08f, transform.position + Vector3.up * 0.08f);
        Gizmos.color = new Color(1f, 0.82f, 0.18f, 0.25f);
        Gizmos.DrawSphere(transform.position, 0.035f);
    }
}
