using UnityEngine;
using Mirror;

public class CameraFollow : NetworkBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Only set the camera for the local player
        if (isLocalPlayer)
        {
            mainCamera = Camera.main; // Get the main camera in the scene

            if (mainCamera != null)
            {
                mainCamera.GetComponent<CameraController>()?.SetTarget(transform);
            }
        }
    }
}
