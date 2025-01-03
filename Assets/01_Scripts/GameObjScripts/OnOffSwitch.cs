using UnityEngine;
using Mirror;

public class OnOffSwitch : Switch
{
    [SerializeField] private OnOffManager onOffManager;

    protected override void OnSwitchStateChanged(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return;

        if (collision.CompareTag("Player"))
        {
            GetComponentInParent<OnOffManager>().OnOffChanged(!IsActivated);
        }
    }

    [ClientRpc]
    public void RpcToggleSwitch(bool newState)
    {
        if (IsActivated != newState)
        {
            ToggleSwitch();
        }
    }
}