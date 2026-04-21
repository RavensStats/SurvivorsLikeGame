using UnityEngine;

public class CameraFollow : MonoBehaviour {
    [Tooltip("The transform the camera will follow")]
    public Transform target;
    [Tooltip("Higher = snappier, lower = smoother (0 = no smoothing)")]
    public float smoothSpeed = 8f;
    [Tooltip("Snap camera X/Y to a pixel grid to prevent tile jitter")]
    public bool pixelSnap = true;
    [Tooltip("Sprite pixels-per-unit used for camera snapping")]
    public float pixelsPerUnit = 16f;

    void Start() {
        if (target != null)
            transform.position = GetSnappedPosition(target.position);
    }

    void LateUpdate() {
        if (target == null) {
            Debug.LogWarning("[CameraFollow] Target is not assigned.", this);
            return;
        }

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        // When snapping is enabled, avoid camera interpolation entirely.
        // Interpolated positions cause sub-frame tile drift, especially at biome boundaries.
        Vector3 next = pixelSnap
            ? desired
            : (smoothSpeed > 0f
                ? Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime)
                : desired);

        transform.position = GetSnappedPosition(next);
    }

    Vector3 GetSnappedPosition(Vector3 worldPos) {
        if (!pixelSnap || pixelsPerUnit <= 0f) return worldPos;

        float unit = 1f / pixelsPerUnit;
        worldPos.x = Mathf.Round(worldPos.x / unit) * unit;
        worldPos.y = Mathf.Round(worldPos.y / unit) * unit;
        worldPos.z = transform.position.z;
        return worldPos;
    }
}
