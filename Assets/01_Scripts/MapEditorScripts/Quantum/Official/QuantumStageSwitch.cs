using UnityEngine;

namespace FlipFriends.Maps
{
    [DisallowMultipleComponent]
    public sealed class QuantumStageSwitch : MonoBehaviour
    {
        [SerializeField] private QuantumStageTerrainToggle target;
        [SerializeField, Tooltip("벨트만 있는 그룹을 반전할 때 target 대신 그 그룹의 벨트를 지정한다.")] private QuantumStageConveyor conveyor;
        [SerializeField] private bool isDoor;
        [SerializeField] private bool detectPlayers = true;
        [SerializeField] private bool detectBoxes;
        [SerializeField] private float cooldown = .5f;

        public QuantumStageTerrainToggle Target => target;
        public QuantumStageConveyor Conveyor => conveyor;

        public QuantumStageSwitchSource Read(string id, Vector2 size) => new()
        {
            id = id, targetGroup = target != null ? target.Group : conveyor.Group, center = transform.position, size = size,
            isDoor = isDoor, detectPlayers = detectPlayers, detectBoxes = detectBoxes,
            cooldown = isDoor ? 0 : cooldown,
        };
    }
}
