using Mirror;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    public List<GameObject> stageMapPrefabs;

    public Transform startPos;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDestroy()
    {
        instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        StageLoad();
    }

    [Server]
    public void StageLoad()
    {
        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;

        if (slimeRoomManager == null)
            return; 

        int stage = slimeRoomManager.currentStage - 1;

        GameObject stageObject = Instantiate(stageMapPrefabs[stage]);
        NetworkServer.Spawn(stageObject);
    }
}
