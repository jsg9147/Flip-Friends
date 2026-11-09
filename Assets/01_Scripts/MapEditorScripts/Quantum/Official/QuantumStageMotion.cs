using UnityEngine;

namespace FlipFriends.Maps
{
    public enum QuantumMotionKind { Platform, MovingTrap, RotatingTrap, PathTrap }

    [DisallowMultipleComponent]
    public sealed class QuantumStageMotion : MonoBehaviour
    {
        [SerializeField] private QuantumMotionKind kind;
        [SerializeField] private Vector2[] waypoints = { Vector2.zero, Vector2.right * 4 };
        [SerializeField] private float speed = 1;
        [SerializeField] private float waitTime;
        [SerializeField] private bool cyclic = true;
        [SerializeField] private bool repeat;
        [SerializeField] private float returnDelay;
        [SerializeField, Range(0, 2)] private float easeAmount;
        [SerializeField] private bool detectPlayer;
        [SerializeField] private float initialWaitTime;
        [SerializeField] private bool disappearAfterComplete;
        [SerializeField] private bool disableColliderWhenFalling;
        [SerializeField] private float rotationSpeed;
        [SerializeField] private bool counterclockwise;
        [SerializeField] private bool randomRotation;
        [SerializeField] private float randomRange;
        public QuantumMotionSource Read(string id, Vector2 size) => new()
        {
            id = id, origin = transform.position, size = size, kind = (int)kind, waypoints = waypoints,
            speed = speed, waitTime = waitTime, cyclic = cyclic, repeat = repeat, returnDelay = returnDelay,
            easeAmount = easeAmount, detectPlayer = detectPlayer, initialWaitTime = initialWaitTime,
            disappearAfterComplete = disappearAfterComplete, disableColliderWhenFalling = disableColliderWhenFalling,
            rotationSpeed = rotationSpeed * (counterclockwise ? -1 : 1), randomRotation = randomRotation, randomRange = randomRange,
        };
    }
}
