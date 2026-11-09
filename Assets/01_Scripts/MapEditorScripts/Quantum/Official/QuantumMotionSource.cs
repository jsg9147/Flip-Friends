using System;
using UnityEngine;

namespace FlipFriends.Maps
{
    [Serializable]
    public sealed class QuantumMotionSource
    {
        public string id;
        public int kind;
        public Vector2 origin, size;
        public Vector2[] waypoints;
        public float speed, waitTime, returnDelay, easeAmount, initialWaitTime, rotationSpeed, randomRange;
        public bool cyclic, repeat, detectPlayer, disappearAfterComplete, disableColliderWhenFalling, randomRotation;
        public Vector2 renderCenter, renderSize;
        public Sprite sprite;
        public string toggleGroup;
        public float toggleInterval;
        public bool initialActive;
    }

    [Serializable]
    public sealed class QuantumMotionResetSource
    {
        public string id, target;
        public Vector2 center, size;
        public bool isTrigger;
    }

    [Serializable]
    public sealed class QuantumConveyorSource
    {
        public string id, group;
        public float interval;
        public bool initialClockwise;
    }

    [Serializable]
    public sealed class QuantumStageSwitchSource
    {
        public string id, targetGroup;
        public Vector2 center, size;
        public float cooldown;
        public bool isDoor, detectPlayers, detectBoxes;
    }
}
