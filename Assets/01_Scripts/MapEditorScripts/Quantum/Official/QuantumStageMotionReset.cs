using UnityEngine;

namespace FlipFriends.Maps
{
    [DisallowMultipleComponent]
    public sealed class QuantumStageMotionReset : MonoBehaviour
    {
        [SerializeField] private QuantumStageMotion target;
        public QuantumStageMotion Target => target;
    }
}
