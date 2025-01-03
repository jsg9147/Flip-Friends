using Mirror;
using UnityEngine;

public class DoorSwitch : LayerBasedSwitch
{
    public GameObject door;

    public override void OnTriggerEnter2D(Collider2D collision)
    {
        base.OnTriggerEnter2D(collision);
    }

    public override void OnTriggerExit2D(Collider2D other)
    {
        base.OnTriggerExit2D(other);
    }
    protected override void OnSwitchStateChanged(bool newState)
    {
        if (!isServer)
            return;

        base.OnSwitchStateChanged(newState);
        RpcDoorStateChanged(newState);
    }

    [ClientRpc]
    private void RpcDoorStateChanged(bool newState)
    {
        //door.SetActive(!newState);
        door.GetComponent<SpriteRenderer>().enabled = !newState;
        door.GetComponent<Collider2D>().enabled = !newState;
    }
}
