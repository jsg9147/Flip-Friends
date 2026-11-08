using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FlipFriends.Maps
{
    // Main에서 고를 수 있는 공식 스테이지 목록. 배열 순서가 표시·선택 순서다.
    public sealed class QuantumOfficialStageCatalog : ScriptableObject
    {
        public const string ResourceName = "FlipFriendsOfficialStageCatalog";
        [SerializeField] private QuantumOfficialStageAsset[] stages = Array.Empty<QuantumOfficialStageAsset>();
        public IReadOnlyList<QuantumOfficialStageAsset> Stages => stages;

        public void Configure(QuantumOfficialStageAsset[] value) => stages = value ?? Array.Empty<QuantumOfficialStageAsset>();

        public static IReadOnlyList<QuantumOfficialStageAsset> LoadStages()
        {
            var catalog = Resources.Load<QuantumOfficialStageCatalog>(ResourceName);
            if (catalog == null) return Array.Empty<QuantumOfficialStageAsset>();
            if (catalog.stages.Any(s => s == null) || catalog.stages.Select(s => s.StageId).Distinct(StringComparer.Ordinal).Count() != catalog.stages.Length)
                throw new InvalidOperationException("공식 스테이지 목록에 빈 항목이나 중복 ID가 있습니다.");
            return catalog.stages;
        }
    }
}
