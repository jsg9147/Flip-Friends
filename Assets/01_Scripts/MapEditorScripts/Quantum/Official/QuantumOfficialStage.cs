using UnityEngine;

namespace FlipFriends.Maps
{
    // 공식 스테이지 프리팹 루트 표식. 런타임에는 생성하지 않고 작성기만 읽는다.
    [DisallowMultipleComponent]
    public sealed class QuantumOfficialStage : MonoBehaviour
    {
        [SerializeField] private string stageId = "official-";
        [SerializeField] private string displayName = "공식 스테이지";
        public string StageId => stageId;
        public string DisplayName => displayName;
    }
}
