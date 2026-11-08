using UnityEngine;

namespace FlipFriends.Maps
{
    // 수직 로프 영역. 위치가 중심이며 회전 없는 사각만 지원한다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageRope : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new(1, 4);
        public Vector2 Size => size;
        private void OnDrawGizmos() { Gizmos.color = new Color(0.85f, 0.65f, 0.3f); Gizmos.DrawWireCube(transform.position, size); }
    }
}
