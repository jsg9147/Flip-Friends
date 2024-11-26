using NUnit.Framework;
using UnityEngine;
using System;

public class CameraController : MonoBehaviour
{
    public Transform target; // The object that the camera will follow
    public float smoothSpeed = 0.125f; // Camera smoothness
    public Vector3 offset; // Offset from the target position
    private const float FixedZ = -10f; // Fixed Z position for the camera

    private PlayerController2D[] playerControllers;

    // Initializes the list of player controllers in the scene
    private void InitializePlayerControllers()
    {
        playerControllers = FindObjectsByType<PlayerController2D>(FindObjectsSortMode.InstanceID);
        Debug.Log($"Found PlayerControllers: {playerControllers.Length}");

        foreach (var playerController in playerControllers)
        {
            Debug.Log($"PlayerController found on: {playerController.gameObject.name}");
        }
    }

    // Sets a new target for the camera
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Moves the camera to the next or previous target based on direction
    public void MoveNextTarget(float direction)
    {
        if (playerControllers.Length <= 0)
        {
            InitializePlayerControllers();
        }

        int currentIndex = Array.IndexOf(playerControllers, target?.GetComponent<PlayerController2D>());
        int nextIndex = GetNextTargetIndex(currentIndex, direction);

        target = playerControllers[nextIndex].transform;
    }

    // Calculates the next target index in a circular manner
    private int GetNextTargetIndex(int currentIndex, float direction)
    {
        if (direction > 0)
            currentIndex++;
        else if (direction < 0)
            currentIndex--;

        if (currentIndex >= playerControllers.Length)
            currentIndex = 0;
        else if (currentIndex < 0)
            currentIndex = playerControllers.Length - 1;

        return currentIndex;
    }

    void LateUpdate()
    {
        if (target != null)
        {
            MoveCameraToTarget();
        }
    }

    // Smoothly moves the camera to the target's position with offset
    private void MoveCameraToTarget()
    {
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Fix the Z position
        smoothedPosition.z = FixedZ;

        transform.position = smoothedPosition;
    }
}
