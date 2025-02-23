using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class OnOffManager : NetworkBehaviour
{
    [SerializeField] private List<OnOffSwitch> switches;
    [SerializeField] private List<Conveyor> conveyorBelts;

    [SerializeField] private GameObject activeSwitchGround;
    [SerializeField] private GameObject inactiveSwitchGround;

    [SerializeField] private bool autoToggleEnabled = false;
    [SerializeField] private float autoToggleInterval = 3.0f;

    private bool isActivated;

    private void Start()
    {
        isActivated = false;
        if (isServer)
        {
            RpcUpdateSwitchState(isActivated);
            if (autoToggleEnabled)
            {
                StartCoroutine(AutoToggleCoroutine());
            }
        }
    }

    public void ToggleOnOffState(bool newState)
    {
        foreach (var switchUnit in switches)
        {
            switchUnit.RpcToggleSwitch(newState);
        }
        RpcUpdateSwitchState(newState);
    }

    [ClientRpc]
    private void RpcUpdateSwitchState(bool newState)
    {
        isActivated = newState;

        if(activeSwitchGround != null)
            activeSwitchGround.SetActive(newState);
        if(inactiveSwitchGround != null)
            inactiveSwitchGround.SetActive(!newState);

        foreach (var conveyor in conveyorBelts)
        {
            if(conveyor != null)
                conveyor.UpdateConveyorState(newState);
        }
    }

    private IEnumerator AutoToggleCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoToggleInterval);
            ToggleOnOffState(!isActivated);
        }
    }
}
