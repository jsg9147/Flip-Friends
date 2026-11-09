using UnityEngine;

namespace FlipFriends.Maps
{
    [DisallowMultipleComponent]
    public sealed class QuantumStageTerrainToggle : MonoBehaviour
    {
        [SerializeField] private string group = "terrain";
        [SerializeField] private float interval = 3;
        [SerializeField] private bool initialActive = true;
        public string Group => group;
        public QuantumMotionSource Read(string id, Vector2 size) => new()
        {
            id = id, kind = 4, origin = transform.position, size = size,
            waypoints = new[] { Vector2.zero, Vector2.zero }, toggleGroup = group, toggleInterval = interval, initialActive = initialActive,
        };
    }
}
