using Mirror;
using UnityEngine;

public abstract class Switch : NetworkBehaviour
{
    [SyncVar] // 네트워크 싱크를 위한 상태 변수
    private bool isActivated = false;

    public Sprite onSwitchSprite;
    public Sprite offSwitchSprite;

    // 스위치 활성화 상태 접근자
    public bool IsActivated => isActivated;

    // 스위치를 활성화 또는 비활성화하는 메서드
    public void ToggleSwitch()
    {
        isActivated = !isActivated;
        OnSwitchStateChanged(isActivated);
    }

    // 스위치 상태 변경 이벤트 (상속받아 재정의 필요)
    protected abstract void OnSwitchStateChanged(bool newState);
}
