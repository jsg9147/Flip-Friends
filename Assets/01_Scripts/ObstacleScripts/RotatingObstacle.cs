using Mirror;
using UnityEngine;

public class RotatingObstacle : BasicTrap
{
    public bool isCounterclockwise;
    public float rotationSpeed = 120f;
    public bool random;
    public float randomRange;

    [SyncVar] private float serverRotationSpeed;
    [SyncVar] private bool serverCounterclockwise;
    private bool hasServerSelection;

    public float SelectedRotationSpeed => serverRotationSpeed;
    public bool SelectedCounterclockwise => serverCounterclockwise;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (hasServerSelection) return;
        if (!TryValidateConfiguration(out string problem))
        {
            Debug.LogError($"{transform.root.name}/{name}: {problem}", this);
            return;
        }
        serverRotationSpeed = rotationSpeed;
        serverCounterclockwise = isCounterclockwise;
        if (random)
        {
            serverRotationSpeed += Random.Range(-randomRange, randomRange);
            serverCounterclockwise = Random.Range(0f, 1f) >= .5f;
        }
        hasServerSelection = true;
    }

    private void Update()
    {
        if (!isServer || !hasServerSelection) return;
        transform.Rotate(Vector3.forward, RotationDelta(Time.deltaTime));
    }

    public float RotationDelta(float deltaTime) => (serverCounterclockwise ? -1 : 1) * serverRotationSpeed * deltaTime;

    public bool TryValidateConfiguration(out string problem)
    {
        problem = !float.IsNaN(rotationSpeed) && !float.IsInfinity(rotationSpeed) &&
                  !float.IsNaN(randomRange) && !float.IsInfinity(randomRange) && randomRange >= 0
            ? null : "회전 속도/랜덤 범위 설정이 잘못되었습니다.";
        return problem == null;
    }
}
