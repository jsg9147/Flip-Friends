using UnityEngine;

namespace FlipFriends.Maps
{
    // 정적 피격 트랩. 같은 오브젝트의 트리거 BoxCollider2D 또는 단일 경로 볼록 PolygonCollider2D가 판정이다.
    // 방향이 0이면 원본 Trap 태그 판정처럼 트랩 위치→플레이어 위치 방향으로, 아니면 원본 BasicTrap.knockbackDir처럼 이 값을 그대로 쓴다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageTrap : MonoBehaviour
    {
        [SerializeField] private Vector2 knockbackDirection;
        public Vector2 KnockbackDirection => knockbackDirection;
    }
}
