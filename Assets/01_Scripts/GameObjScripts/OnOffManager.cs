using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class OnOffManager : NetworkBehaviour
{
    private const string ActiveSwitchGroundName = "OnSwitchGround";
    private const string InactiveSwitchGroundName = "OffSwitchGround";

    [SerializeField] private List<OnOffSwitch> switches = new List<OnOffSwitch>();
    [SerializeField] private List<Conveyor> conveyorBelts = new List<Conveyor>();
    [SerializeField] private GameObject activeSwitchGround;
    [SerializeField] private GameObject inactiveSwitchGround;
    [SerializeField] private bool autoToggleEnabled = false;
    [SerializeField] private float autoToggleInterval = 3.0f;

    [SyncVar(hook = nameof(OnActivatedChanged))] private bool isActivated;
    private Coroutine autoToggleCoroutine;

    public bool IsActivated => isActivated;

    private void Awake() => ResolveMissingGroundReferences();

    public override void OnStartServer()
    {
        base.OnStartServer();
        isActivated = false;
        ApplyState(isActivated);
        if (autoToggleEnabled)
            autoToggleCoroutine = StartCoroutine(AutoToggleCoroutine());
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyGroundState(isActivated);
    }

    public override void OnStopServer()
    {
        if (autoToggleCoroutine != null) StopCoroutine(autoToggleCoroutine);
        autoToggleCoroutine = null;
        base.OnStopServer();
    }

    [Server]
    public void ToggleState()
    {
        if (!isServer) return;
        ToggleOnOffState(!isActivated);
    }

    [Server]
    public void ToggleOnOffState(bool newState)
    {
        if (!isServer) return;
        isActivated = newState;
        ApplyState(newState);
    }

    private void OnActivatedChanged(bool oldState, bool newState) => ApplyGroundState(newState);

    private void ApplyState(bool newState)
    {
        ApplyGroundState(newState);
        foreach (OnOffSwitch switchUnit in switches)
        {
            if (switchUnit != null && switchUnit.Manager == this)
                switchUnit.SetSwitchState(newState);
        }
        foreach (Conveyor conveyor in conveyorBelts)
        {
            if (conveyor != null) conveyor.UpdateConveyorState(newState);
        }
    }

    private void ApplyGroundState(bool newState)
    {
        if (activeSwitchGround != null) activeSwitchGround.SetActive(newState);
        if (inactiveSwitchGround != null) inactiveSwitchGround.SetActive(!newState);
    }

    private void ResolveMissingGroundReferences()
    {
        if (activeSwitchGround != null && inactiveSwitchGround != null) return;
        Transform stageRoot = transform.parent;
        if (stageRoot == null || stageRoot.GetComponentsInChildren<OnOffManager>(true).Length != 1) return;
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
            yield return new WaitForSeconds(Mathf.Max(0.01f, autoToggleInterval));
            ToggleState();
        }
    }
}
