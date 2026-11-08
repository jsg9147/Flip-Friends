using UnityEngine;

namespace FlipFriends.Maps
{
    // 도착 판정 영역. 위치가 중심이며 Collider 대신 크기를 직접 둔다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageGoal : MonoBehaviour
    {
        [SerializeField] private Vector2 size = Vector2.one;
        public Vector2 Size => size;
        private void OnDrawGizmos() { Gizmos.color = Color.green; Gizmos.DrawWireCube(transform.position, size); }
    }
}
