using UnityEngine;
using Mirror;

public class OnOffSwitch : Switch
{
    [SerializeField] private OnOffManager onOffManager;

    private bool isSyncingState = false; // 무한 루프 방지 플래그
    private float lastToggleTime = 0f;   // 마지막으로 스위치를 작동한 시간
    [SerializeField] private float cooldown = 0.5f; // 스위치 재사용 대기 시간 (초)

    protected override void OnSwitchStateChanged(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }

    private void Update()
    {
        if (Time.time - lastToggleTime < cooldown) return; // 쿨다운 시간이 지나지 않았으면 동작하지 않음
        DetectPlayer();
    }

    public override void ToggleSwitch()
    {
        if (isSyncingState) return; // 상태 동기화 중이면 동작하지 않음
        lastToggleTime = Time.time; // 마지막 동작 시간 업데이트
        base.ToggleSwitch();
        OnOffChange();
    }

    private void OnOffChange()
    {
        if (!isServer)
            return;

        GetComponentInParent<OnOffManager>().OnOffChanged(IsActivated);
        RpcToggleSwitch(IsActivated); // 클라이언트와 상태 동기화
    }

    [ClientRpc]
    public void RpcToggleSwitch(bool newState)
    {
        if (isSyncingState) return; // 상태 동기화 중이면 동작하지 않음

        isSyncingState = true; // 상태 동기화 시작
        if (IsActivated != newState)
        {
            base.ToggleSwitch(); // 부모 클래스의 ToggleSwitch 호출
        }
        isSyncingState = false; // 상태 동기화 종료
    }
}
