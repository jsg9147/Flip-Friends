using System;
using Quantum;
using UnityEngine;

namespace FlipFriends.Maps
{
    // 작성기가 저장한 공식 스테이지 콘텐츠. 시작할 때마다 원본 값에서 변환을 다시 만들어 저장 해시와 대조한다.
    public sealed class QuantumOfficialStageAsset : ScriptableObject, IQuantumMapContent
    {
        [SerializeField] private string displayName;
        [SerializeField] private QuantumOfficialStageData data;
        [SerializeField] private Quantum.Map bakedMap;
        [SerializeField] private QuantumMapTerrain terrain;
        [SerializeField] private QuantumMapDevices devices;
        [SerializeField] private string contentHash;

        public string DisplayName => displayName;
        public string StageId => data?.stageId;
        public string ContentHash => contentHash;
        public string Agreement => $"공식{QuantumOfficialStageConverter.FormatVersion}:{contentHash}";

        public void Configure(string name, QuantumOfficialStageData value, Quantum.Map map, QuantumMapTerrain stageTerrain, QuantumMapDevices stageDevices)
        {
            displayName = name; data = value; bakedMap = map; terrain = stageTerrain; devices = stageDevices;
            contentHash = QuantumOfficialStageConverter.Convert(data, terrain, devices).ContentHash;
        }

        public QuantumMapConversion ValidateAndApply(RuntimeConfig config)
        {
            if (bakedMap == null || string.IsNullOrEmpty(contentHash) || string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException($"공식 스테이지 {StageId}: Quantum 맵·해시·표시 이름 연결이 없습니다.");
            var conversion = QuantumOfficialStageConverter.Convert(data, terrain, devices);
            // 공식 지형은 FP 윤곽으로만 전달하므로 SDK 정적 Collider가 섞이면 같은 콘텐츠로 보지 않는다.
            if (conversion.ContentHash != contentHash || bakedMap.StaticColliders2D.Length != 0 ||
                bakedMap.MapEntities.Length != 0 || bakedMap.StaticColliders3D.Length != 0)
                throw new InvalidOperationException($"공식 스테이지 {StageId}: 콘텐츠 해시 또는 베이크 구성이 일치하지 않습니다.");
            conversion.ApplyTo(config, bakedMap, Agreement);
            return conversion;
        }

        public void ApplyRespawn(RuntimeConfig config, QuantumMapConversion conversion) => conversion.ApplyFallBoundary(config);
    }
}
