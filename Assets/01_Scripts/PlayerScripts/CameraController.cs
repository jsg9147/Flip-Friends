using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset;
    private const float FixedZ = -10f;

    private PlayerController2D[] playerControllers;
    private Vector3 velocity = Vector3.zero;

    private bool canMoveTarget = true; // 타겟 변경 가능 여부
    public float targetSwitchDelay = 0.5f; // 딜레이 시간 (초)

    private void InitializePlayerControllers()
    {
        playerControllers = FindObjectsByType<PlayerController2D>();
        Debug.Log($"Found PlayerControllers: {playerControllers.Length}");

        foreach (var playerController in playerControllers)
        {
            Debug.Log($"PlayerController found on: {playerController.gameObject.name}");
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void MoveNextTarget(float direction)
    {
        if (!canMoveTarget) return; // 딜레이 중이면 실행하지 않음

        if (playerControllers == null || playerControllers.Length <= 0)
        {
            InitializePlayerControllers();
        }

        int currentIndex = Array.IndexOf(playerControllers, target?.GetComponent<PlayerController2D>());
        int nextIndex = GetNextTargetIndex(currentIndex, direction);

        target = playerControllers[nextIndex].transform;

        StartCoroutine(DelayTargetSwitch()); // 딜레이 시작
    }

    private IEnumerator DelayTargetSwitch()
    {
        canMoveTarget = false; // 타겟 변경 불가
        yield return new WaitForSeconds(targetSwitchDelay); // 딜레이
        canMoveTarget = true; // 타겟 변경 가능
    }

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

    private void MoveCameraToTarget()
    {
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothSpeed);
        smoothedPosition.z = FixedZ;
        transform.position = smoothedPosition;
    }
}
