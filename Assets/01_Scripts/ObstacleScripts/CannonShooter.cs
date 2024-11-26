using UnityEngine;
using Mirror;
using System.Collections;

public class CannonShooter : NetworkBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireInterval = 2.0f;
    [SerializeField] private float projectileSpeed = 10.0f;
    [SerializeField] private float projectileLifetime = 5.0f;
    [SerializeField] private Vector2 fireDirection = Vector2.right;

    private void Start()
    {
        if (isServer)
        {
            InvokeRepeating(nameof(FireProjectile), 1.0f, fireInterval);
        }
    }

    [Server]
    private void FireProjectile()
    {
        GameObject projectileInstance = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        NetworkServer.Spawn(projectileInstance);
        RpcLaunchProjectile(projectileInstance);
        StartCoroutine(DestroyAfterTime(projectileInstance, projectileLifetime));
    }

    [ClientRpc]
    private void RpcLaunchProjectile(GameObject projectile)
    {
        if (projectile != null)
        {
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = fireDirection.normalized * projectileSpeed;
            }
        }
    }

    private IEnumerator DestroyAfterTime(GameObject projectile, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (projectile != null && projectile.GetComponent<NetworkIdentity>().isServer)
        {
            NetworkServer.Destroy(projectile);
        }
    }
}

// Note: Attach this script to a child object that has a NetworkIdentity component.
// Make sure the 'projectilePrefab' has a NetworkIdentity and Rigidbody2D attached to it.
