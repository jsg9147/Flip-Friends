using UnityEngine;
using Mirror;
using System.Collections.Generic;


public class OnOffManager : NetworkBehaviour
{
    [SerializeField] private List<OnOffSwitch> onOffSwitchs;

    public GameObject onSwitchGround;
    public GameObject offSwitchGround;

    private bool isActive;

    private void Start()
    {
        isActive = false;
        if(isServer)
            RpcGroundChanged(isActive);
    }

    public void OnOffChanged(bool newState)
    {
        foreach (var onOffSwitch in onOffSwitchs)
        {
            onOffSwitch.RpcToggleSwitch(newState);
        }
        RpcGroundChanged(newState);
    }

    [ClientRpc]
    private void RpcGroundChanged(bool newState)
    {
        isActive = newState;
        onSwitchGround.SetActive(newState);
        offSwitchGround.SetActive(!newState);
    }
}
