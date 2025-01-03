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
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(2);
        }
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

        // NetworkIdentity 컴포넌트 가져오기
        NetworkIdentity stageIdentity = stageObject.GetComponent<NetworkIdentity>();

        if (stageIdentity != null)
        {
            // 서버가 권한을 가지게 하기 위해 Spawn
            NetworkServer.Spawn(stageObject);
        }
        else
        {
            Debug.LogError("스테이지 오브젝트에 NetworkIdentity 컴포넌트가 없습니다.");
        }
    }

}
