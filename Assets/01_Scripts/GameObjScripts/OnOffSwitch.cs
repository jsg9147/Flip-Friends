using UnityEngine;
using Mirror;

public class OnOffSwitch : Switch
{
    [SerializeField] private OnOffManager onOffManager;

    private bool isStateSyncing = false; // 무한 루프 방지 플래그
    private float lastSwitchToggleTime = 0f; // 마지막으로 스위치를 작동한 시간
    [SerializeField] private float toggleCooldown = 0.5f; // 스위치 재사용 대기 시간 (초)

    protected override void OnSwitchStateChanged(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }

    private void Update()
    {
        if (Time.time - lastSwitchToggleTime < toggleCooldown) return;
        DetectPlayer();
    }

    public override void ToggleSwitch()
    {
        if (isStateSyncing) return;
        lastSwitchToggleTime = Time.time;
        base.ToggleSwitch();
        NotifyOnOffManager();
    }

    private void NotifyOnOffManager()
    {
        if (!isServer) return;

        onOffManager.ToggleOnOffState(IsActivated);
        RpcToggleSwitch(IsActivated);
    }

    [ClientRpc]
    public void RpcToggleSwitch(bool newState)
    {
        if (isStateSyncing) return;

        isStateSyncing = true;
        if (IsActivated != newState)
        {
            base.ToggleSwitch();
        }
        isStateSyncing = false;
    }
}
