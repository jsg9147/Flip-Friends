using UnityEngine;

namespace FlipFriends.Maps
{
    // 운반 상자의 초기 위치. 크기·중력은 현재 지원하는 원본 상자 규칙(0.9, 9.8)으로 고정한다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageBox : MonoBehaviour
    {
        public const float Size = 0.9f;
        private void OnDrawGizmos() { Gizmos.color = new Color(0.9f, 0.6f, 0.2f); Gizmos.DrawWireCube(transform.position, Vector3.one * Size); }
    }
}
