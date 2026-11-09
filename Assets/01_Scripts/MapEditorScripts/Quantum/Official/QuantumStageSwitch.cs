using UnityEngine;

namespace FlipFriends.Maps
{
    [DisallowMultipleComponent]
    public sealed class QuantumStageSwitch : MonoBehaviour
    {
        [SerializeField] private QuantumStageTerrainToggle target;
        [SerializeField] private bool isDoor;
        [SerializeField] private bool detectPlayers = true;
        [SerializeField] private bool detectBoxes;
        [SerializeField] private float cooldown = .5f;

        public QuantumStageTerrainToggle Target => target;

        public QuantumStageSwitchSource Read(string id, Vector2 size) => new()
        {
            id = id, targetGroup = target.Group, center = transform.position, size = size,
            isDoor = isDoor, detectPlayers = detectPlayers, detectBoxes = detectBoxes,
            cooldown = isDoor ? 0 : cooldown,
        };
    }
}
