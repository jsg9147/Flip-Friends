using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class OnOffManager : NetworkBehaviour
{
    private const string ActiveSwitchGroundName = "OnSwitchGround";
    private const string InactiveSwitchGroundName = "OffSwitchGround";

    [SerializeField] private List<OnOffSwitch> switches;
    [SerializeField] private List<Conveyor> conveyorBelts;

    [SerializeField] private GameObject activeSwitchGround;
    [SerializeField] private GameObject inactiveSwitchGround;

    [SerializeField] private bool autoToggleEnabled = false;
    [SerializeField] private float autoToggleInterval = 3.0f;

    private bool isActivated;

    private void Awake()
    {
        ResolveMissingGroundReferences();
    }

    private void Start()
    {
        isActivated = false;
        UpdateSwitchState(isActivated);

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

        UpdateSwitchState(newState);
        RpcUpdateSwitchState(newState);
    }

    [ClientRpc]
    private void RpcUpdateSwitchState(bool newState)
    {
        UpdateSwitchState(newState);
    }

    private void UpdateSwitchState(bool newState)
    {
        isActivated = newState;

        if (activeSwitchGround != null)
            activeSwitchGround.SetActive(newState);
        if (inactiveSwitchGround != null)
            inactiveSwitchGround.SetActive(!newState);

        foreach (var conveyor in conveyorBelts)
        {
            if (conveyor != null)
                conveyor.UpdateConveyorState(newState);
        }
    }

    private void ResolveMissingGroundReferences()
    {
        if (activeSwitchGround != null && inactiveSwitchGround != null)
            return;

        Transform stageRoot = transform.parent;
        if (stageRoot == null)
            return;

        OnOffManager[] managers = stageRoot.GetComponentsInChildren<OnOffManager>(true);
        if (managers.Length != 1)
            return;

        foreach (Transform child in stageRoot.GetComponentsInChildren<Transform>(true))
        {
            if (activeSwitchGround == null && child.name == ActiveSwitchGroundName)
                activeSwitchGround = child.gameObject;
            else if (inactiveSwitchGround == null && child.name == InactiveSwitchGroundName)
                inactiveSwitchGround = child.gameObject;
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
