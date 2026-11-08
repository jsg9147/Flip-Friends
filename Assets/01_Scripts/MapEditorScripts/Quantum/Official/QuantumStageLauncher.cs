using UnityEngine;

namespace FlipFriends.Maps
{
    public enum QuantumLauncherKind { Bounce, Spring }

    // 원본 태그가 선택하던 속도 덮어쓰기 종류를 공식 맵 작성 데이터로 지정한다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageLauncher : MonoBehaviour
    {
        [SerializeField] private QuantumLauncherKind kind = QuantumLauncherKind.Bounce;
        public QuantumLauncherKind Kind => kind;
    }
}
