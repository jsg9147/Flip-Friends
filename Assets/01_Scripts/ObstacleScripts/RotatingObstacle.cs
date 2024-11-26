using UnityEngine;
using Mirror;

public class RotatingObstacle : BasicTrap
{
    public bool isCounterclockwise;
    public float rotationSpeed = 120f; // 초당 회전 속도

    void Update()
    {
        // 서버에서만 회전 로직 실행
        if (isServer)
        {
            RotateObstacle();
        }
    }

    [Server]
    private void RotateObstacle()
    {
        Vector3 rotateDir = isCounterclockwise ? Vector3.back : Vector3.forward;

        // 장애물 회전
        transform.Rotate(rotateDir, rotationSpeed * Time.deltaTime);
    }
}
