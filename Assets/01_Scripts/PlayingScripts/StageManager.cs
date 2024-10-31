using Mirror;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : NetworkBehaviour
{
    public static StageManager instance;

    public List<GameObject> stageMapPrefabs;

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

    private void Start()
    {
        if (isServer)
            StageLoad();
    }

    [Server]
    public void StageLoad()
    {
        int stage = MirrorRoomManager.Instance.currentStage - 1;
        GameObject stageObject = stageMapPrefabs[stage];
        stageObject.SetActive(true);

        // 모든 클라이언트에서 이 오브젝트를 활성화하도록 호출
        RpcActivateStageObject(stage);
    }

    [ClientRpc]
    private void RpcActivateStageObject(int stage)
    {
        // 클라이언트에서 특정 오브젝트 활성화
        if (stageMapPrefabs[stage] != null)
        {
            stageMapPrefabs[stage].SetActive(true);
        }
    }
}
