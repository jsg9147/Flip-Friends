using Mirror;
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

        int stage = slimeRoomManager.currentStage;

        GameObject stageObject = Instantiate(stageMapPrefabs[stage]);

        // NetworkIdentity ������Ʈ ��������
        NetworkIdentity stageIdentity = stageObject.GetComponent<NetworkIdentity>();

        if (stageIdentity != null)
        {
            // ������ ������ ������ �ϱ� ���� Spawn
            NetworkServer.Spawn(stageObject);
        }
        else
        {
            Debug.LogError("�������� ������Ʈ�� NetworkIdentity ������Ʈ�� �����ϴ�.");
        }
    }

}
