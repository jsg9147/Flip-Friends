using UnityEngine;

namespace FlipFriends.Maps
{
    // 시계 방향은 윗면이 왼쪽으로 흐르는 원본 isClockwise=true다. 원본 OnOffManager 그룹 벨트는 반시계(오른쪽)에서 시작했다.
    public enum QuantumConveyorDirection { Clockwise, Counterclockwise }

    // 고정 벨트는 이 컴포넌트만, 이동 벨트는 같은 오브젝트에 Platform 종류 QuantumStageMotion을 함께 둔다.
    [DisallowMultipleComponent]
    public sealed class QuantumStageConveyor : MonoBehaviour
    {
        [SerializeField] private QuantumConveyorDirection initialDirection;
        [SerializeField, Tooltip("비우면 방향 고정. 전환 지형과 같은 이름이면 그 그룹의 적용 상태를 따른다.")] private string group;
        [SerializeField, Tooltip("벨트만 있는 그룹의 자동 반전 간격(초). 0이면 수동이며 스위치가 필요하다. 지형 그룹이면 지형 간격과 같아야 한다.")] private float interval;

        public string Group => group;

        public QuantumConveyorSource Read(string id) => new()
        {
            id = id, group = group ?? "", interval = interval,
            initialClockwise = initialDirection == QuantumConveyorDirection.Clockwise,
        };
    }
}
