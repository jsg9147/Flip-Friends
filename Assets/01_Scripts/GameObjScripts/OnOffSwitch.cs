using UnityEngine;
using Mirror;

public class OnOffSwitch : Switch
{
    [SerializeField] private OnOffManager onOffManager;
    [SerializeField] private float toggleCooldown = 0.5f;

    private float lastSwitchToggleTime;
    public OnOffManager Manager => onOffManager;

    private void Update()
    {
        if (!isServer) return;
        if (Time.time - lastSwitchToggleTime < toggleCooldown) return;
        DetectPlayer();
    }

    [Server]
    public override void ToggleSwitch()
    {
        if (!isServer) return;
        if (Time.time - lastSwitchToggleTime < toggleCooldown) return;
        if (onOffManager == null)
        {
            Debug.LogError($"{name}: OnOffManager가 연결되지 않았습니다.", this);
            return;
        }
        lastSwitchToggleTime = Time.time;
        onOffManager.ToggleState();
    }
}
