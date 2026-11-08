using UnityEngine;

namespace FlipFriends.Maps
{
    // 구간 Reset. 같은 오브젝트에 Tilemap 트리거가 있으면 생성된 사각 조각을, 없으면 크기 사각 하나를 판정으로 쓴다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageReset : MonoBehaviour
    {
        [SerializeField] private Transform destination;
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private bool detectPlayers = true;
        [SerializeField] private bool detectBoxes = true;
        [SerializeField] private bool onlyBoxReset;
        public Transform Destination => destination;
        public Vector2 Size => size;
        public bool DetectPlayers => detectPlayers;
        public bool DetectBoxes => detectBoxes;
        public bool OnlyBoxReset => onlyBoxReset;
    }
}
