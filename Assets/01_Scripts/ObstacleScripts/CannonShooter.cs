using Mirror;
using UnityEngine;

public class CannonShooter : StagePartBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float firstShootDelay = 0f;
    [SerializeField] private float shootInterval = 2f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private float maxProjectileDistance = 20f;
    [SerializeField] private Vector2 shootDirection = Vector2.right;

    private bool isScheduled;
    private double nextShootTime;
    private const float MuzzleOffset = 1f;
    public GameObject ProjectilePrefab => projectilePrefab;
    public bool IsScheduled => isScheduled;
    public double NextShootTime => nextShootTime;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Schedule();
    }

    private void OnEnable()
    {
        if (netIdentity != null && isServer) Schedule();
    }

    private void OnDisable() => isScheduled = false;
    public override void OnStopServer()
    {
        isScheduled = false;
        base.OnStopServer();
    }

    private void Schedule()
    {
        if (!isServer || !isActiveAndEnabled || isScheduled) return;
        if (!TryValidateConfiguration(out string problem))
        {
            Debug.LogError($"{transform.root.name}/{name}: {problem}", this);
            return;
        }
        nextShootTime = NetworkTime.time + firstShootDelay;
        isScheduled = true;
    }

    private void Update()
    {
        if (!isServer || !isScheduled || NetworkTime.time < nextShootTime) return;
        ShootProjectile();
        AdvanceSchedule(NetworkTime.time);
    }

    private void AdvanceSchedule(double now)
    {
        nextShootTime += shootInterval;
        if (nextShootTime <= now) nextShootTime = now + shootInterval;
    }

    [Server]
    private void ShootProjectile()
    {
        if (!isServer || !isActiveAndEnabled) return;
        NetworkServer.Spawn(CreateProjectile());
    }

    private GameObject CreateProjectile()
    {
        Vector3 position = transform.position + (Vector3)(shootDirection.normalized * MuzzleOffset);
        GameObject projectile = Instantiate(projectilePrefab, position, Quaternion.identity);
        projectile.GetComponent<CannonProjectile>().ServerInitialize(shootDirection.normalized * projectileSpeed, maxProjectileDistance);
        return projectile;
    }

    public bool TryValidateConfiguration(out string problem)
    {
        problem = null;
        if (projectilePrefab == null || projectilePrefab.GetComponent<NetworkIdentity>() == null ||
            projectilePrefab.GetComponent<Rigidbody2D>() == null || projectilePrefab.GetComponent<CannonProjectile>() == null)
            problem = "투사체의 NetworkIdentity/Rigidbody2D/CannonProjectile 참조가 필요합니다.";
        else if (!projectilePrefab.TryGetComponent(out NetworkTransformBase transformSync) ||
                 transformSync.syncDirection != SyncDirection.ServerToClient || transformSync.target != projectilePrefab.transform)
            problem = "투사체에 서버 권한 NetworkTransform이 필요합니다.";
        else if (!IsFinite(firstShootDelay) || firstShootDelay < 0 || !IsFinite(shootInterval) || shootInterval <= 0 ||
                 !IsFinite(projectileSpeed) || projectileSpeed <= 0 || !IsFinite(maxProjectileDistance) || maxProjectileDistance <= 0 ||
                 !IsFinite(shootDirection.x) || !IsFinite(shootDirection.y) || shootDirection.sqrMagnitude == 0)
            problem = "발사 지연/주기/속도/거리/방향 설정이 잘못되었습니다.";
        return problem == null;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
