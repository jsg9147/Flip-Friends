using System;
using UnityEngine;

namespace FlipFriends.Maps
{
    [Serializable]
    public struct QuantumStageArea
    {
        public Vector2 center;
        public Vector2 size;
    }

    // 작성기가 트랩 Collider에서 읽은 월드 좌표 판정과 표시 원본.
    [Serializable]
    public sealed class QuantumTrapSource
    {
        public string id;
        public Vector2 origin;
        public Vector2 direction;
        public Vector2 center;
        public float radius;
        public Vector2[] vertices;
        public Vector2 renderCenter, renderSize;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class QuantumLauncherSource
    {
        public string id;
        public int kind;
        public Vector2 origin, center;
        public float radius;
        public Vector2[] vertices;
        public Vector2 renderCenter, renderSize;
        public Sprite sprite;
    }

    // 공식 스테이지 프리팹에서 읽은 지형·장치 밖의 배치 원본. 실행 시 이 값으로 변환을 다시 만들어 해시를 대조한다.
    [Serializable]
    public sealed class QuantumOfficialStageData
    {
        public string stageId;
        public Vector2[] spawns;
        public Vector2[] boxes;
        public QuantumStageArea[] goals;
        public QuantumStageArea[] ropes;
        public QuantumTrapSource[] traps;
        public QuantumLauncherSource[] launchers;
        public QuantumMotionSource[] motions;
        public QuantumMotionResetSource[] motionResets;
        public QuantumStageSwitchSource[] switches;
        public Bounds renderBounds;
    }
}
