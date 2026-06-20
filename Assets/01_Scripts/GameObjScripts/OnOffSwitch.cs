using UnityEngine;
using Mirror;

public class OnOffSwitch : Switch
{
    [SerializeField] private OnOffManager onOffManager;

    private float lastSwitchToggleTime = 0f;
    [SerializeField] private float toggleCooldown = 0.5f;

    protected override void OnSwitchStateChanged(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }

    private void Update()
    {
        // 플레이어 감지 및 토글은 서버에서만 — 클라이언트가 독립적으로 SyncVar를 변경하면 서버 상태와 불일치 발생
        if (!isServer) return;
        if (Time.time - lastSwitchToggleTime < toggleCooldown) return;
        DetectPlayer();
    }

    public override void ToggleSwitch()
    {
        lastSwitchToggleTime = Time.time;
        base.ToggleSwitch();
        NotifyOnOffManager();
    }

    private void NotifyOnOffManager()
    {
        if (!isServer) return;

        onOffManager.ToggleOnOffState(IsActivated);
        // SyncVar(isActivated)는 서버가 자동 전파 — RPC는 스프라이트 갱신만 담당
        RpcToggleSwitch(IsActivated);
    }

    [ClientRpc]
    public void RpcToggleSwitch(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }
}
