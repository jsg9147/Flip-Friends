using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target; // The object that the camera will follow
    public float smoothSpeed = 0.125f; // Camera smoothness
    public Vector3 offset; // Offset from the target position
    private float fixedZ = -10f; // Fixed Z position for the camera

    // Set the target for the camera
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

            // Fix the Z position to -10
            smoothedPosition.z = fixedZ;

            transform.position = smoothedPosition;
        }
    }
}
