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

    // 공식 스테이지 프리팹에서 읽은 지형·장치 밖의 배치 원본. 실행 시 이 값으로 변환을 다시 만들어 해시를 대조한다.
    [Serializable]
    public sealed class QuantumOfficialStageData
    {
        public string stageId;
        public Vector2[] spawns;
        public Vector2[] boxes;
        public QuantumStageArea[] goals;
        public QuantumStageArea[] ropes;
        public Bounds renderBounds;
    }
}
