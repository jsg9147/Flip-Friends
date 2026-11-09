using UnityEngine;

namespace FlipFriends.Maps
{
    // 원본 Cannon.prefab처럼 같은 오브젝트의 QuantumStageTrap(트리거 Box)이 몸체 피격 판정이다.
    // 방향은 원본 shootDirection처럼 Transform 회전·배율과 무관한 월드 방향이며 포구는 정규화 방향으로 1유닛 앞이다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageCannon : MonoBehaviour
    {
        [SerializeField, Min(0)] private float firstShootDelay;
        [SerializeField] private float shootInterval = 2f;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float maxProjectileDistance = 20f;
        [SerializeField] private Vector2 shootDirection = Vector2.right;
        [SerializeField, Tooltip("원본 CannonBall의 CircleCollider2D 반지름")] private float projectileRadius = 0.25f;
        [SerializeField] private Sprite projectileSprite;

        public QuantumCannonSource Read(string id) => new()
        {
            id = id, origin = transform.position, direction = shootDirection, delay = firstShootDelay, interval = shootInterval,
            speed = projectileSpeed, distance = maxProjectileDistance, radius = projectileRadius, projectileSprite = projectileSprite,
        };
    }
}
