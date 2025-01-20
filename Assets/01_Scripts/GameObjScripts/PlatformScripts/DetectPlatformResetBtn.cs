using UnityEngine;
using Mirror;

public class DetectPlatformResetBtn : LayerBasedSwitch
{
    public PlayerDetectPlatform detectPlatform;

    public override void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
        if (isServer)
            RpcPlatformReset();
    }

    protected override void OnSwitchStateChanged(bool newState)
    {
        if (!isServer)
            return;

        base.OnSwitchStateChanged(newState);
        RpcPlatformReset();
    }

    [ClientRpc]

    private void RpcPlatformReset()
    {
        detectPlatform.ResetPlatformState();
    }
}
