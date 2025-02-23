using UnityEngine;
using Mirror;
using System.Collections;

public class CannonShooter : NetworkBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float firstShootDelay = 0f;
    [SerializeField] private float shootInterval = 2.0f;
    [SerializeField] private float projectileSpeed = 10.0f;
    [SerializeField] private float maxProjectileDistance = 20.0f;
    [SerializeField] private Vector2 shootDirection = Vector2.right;

    private void Start()
    {
        if (isServer)
        {
            InvokeRepeating(nameof(ShootProjectile), firstShootDelay, shootInterval);
        }
    }

    [Server]
    private void ShootProjectile()
    {
        Vector3 shootPosition = transform.position + (Vector3)(shootDirection.normalized * 1f);
        GameObject projectileInstance = Instantiate(projectilePrefab, shootPosition, Quaternion.identity);
        NetworkServer.Spawn(projectileInstance);
        RpcSetProjectileVelocity(projectileInstance, shootDirection.normalized * projectileSpeed);
        StartCoroutine(DestroyIfOutOfRange(projectileInstance, shootPosition));
    }

    [ClientRpc]
    private void RpcSetProjectileVelocity(GameObject projectile, Vector2 velocity)
    {
        if (projectile.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = velocity;
        }
    }

    private IEnumerator DestroyIfOutOfRange(GameObject projectile, Vector3 startPosition)
    {
        while (projectile != null)
        {
            if (Vector3.Distance(startPosition, projectile.transform.position) > maxProjectileDistance)
            {
                if (projectile.GetComponent<NetworkIdentity>().isServer)
                {
                    NetworkServer.Destroy(projectile);
                }
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}
