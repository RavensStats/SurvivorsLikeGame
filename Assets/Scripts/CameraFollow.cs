using UnityEngine;

public class CameraFollow : MonoBehaviour {
    [Tooltip("The transform the camera will follow")]
    public Transform target;
    [Tooltip("Higher = snappier, lower = smoother")]
    public float smoothSpeed = 8f;

    void Start() {
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }

    void LateUpdate() {
        if (target == null) {
            Debug.LogWarning("[CameraFollow] Target is not assigned.", this);
            return;
        }
        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
