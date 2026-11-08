using UnityEngine;

namespace FlipFriends.Maps
{
    // 체크포인트. 플레이어별로 ID가 큰 쪽으로만 갱신되며 위치가 복귀 목적지다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class QuantumStageCheckpoint : MonoBehaviour
    {
        [SerializeField] private int checkpointId;
        [SerializeField] private Vector2 size = Vector2.one;
        public int CheckpointId => checkpointId;
        public Vector2 Size => size;
    }
}
