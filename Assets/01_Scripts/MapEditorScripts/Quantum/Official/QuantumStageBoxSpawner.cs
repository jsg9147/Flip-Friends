using UnityEngine;

namespace FlipFriends.Maps
{
    // 상자 소환 스위치. 대상 상자를 목적지로 옮기며 막히면 상태·쿨다운을 바꾸지 않는다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class QuantumStageBoxSpawner : MonoBehaviour
    {
        [SerializeField] private QuantumStageBox target;
        [SerializeField] private Transform destination;
        [SerializeField] private float cooldownSeconds = 5;
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private Sprite pressedSprite;
        public QuantumStageBox Target => target;
        public Transform Destination => destination;
        public float CooldownSeconds => cooldownSeconds;
        public Vector2 Size => size;
        public Sprite PressedSprite => pressedSprite;
    }
}
