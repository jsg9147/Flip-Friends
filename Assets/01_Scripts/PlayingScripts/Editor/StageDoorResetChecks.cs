using System;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;

public static class StageDoorResetChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Flip Friends/Stages/Check Doors And Platform Reset Buttons")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
            throw new InvalidOperationException("네트워크가 종료된 편집 모드에서만 검사합니다.");
        GameObject server = null, client = null;
        var active = typeof(NetworkServer).GetProperty("active");
        try
        {
            server = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 15.prefab");
            client = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 15.prefab");
            Initialize(server, true);
            Initialize(client, false);
            active.SetValue(null, true);
            var button = server.GetComponentInChildren<DetectPlatformResetBtn>(true);
            button.Awake();
            button.Start();
            var platform = button.detectPlatform;
            platform.Awake();
            platform.Start();
            platform.OnStartServer();
            var p1 = Contact(server, "PlayerOne", "Player");
            var p2 = Contact(server, "PlayerTwo", "Player");
            var excluded = Contact(server, "BoxContact", "Pickable");
            Mark(platform, true);
            button.OnTriggerEnter2D(excluded);
            Require(IsMarked(platform) && !button.IsActivated, "비대상 레이어 무시");
            platform.GetComponent<BoxCollider2D>().enabled = false;
            button.OnTriggerEnter2D(p1);
            Require(!IsMarked(platform) && button.IsActivated, "누름 시 서버 발판 리셋");
            Require(platform.GetComponent<BoxCollider2D>().enabled, "원격 RPC 실행 없이 서버 충돌 판정 복구");
            Mark(platform, true);
            button.OnTriggerEnter2D(p1);
            button.OnTriggerEnter2D(p2);
            Require(IsMarked(platform), "중복 진입과 동시 접촉은 추가 리셋하지 않음");
            button.OnTriggerExit2D(p1);
            Require(button.IsActivated && IsMarked(platform), "한 명 이탈해도 눌림 유지");
            button.OnTriggerExit2D(p2);
            Require(!button.IsActivated && IsMarked(platform), "해제 시 발판 재리셋하지 않음");
            button.OnTriggerEnter2D(p2);
            Require(!IsMarked(platform), "다시 누르면 한 번 리셋");
            Mark(platform, true);
            p2.enabled = false;
            typeof(LayerBasedSwitch).GetMethod("FixedUpdate", Hidden).Invoke(button, null);
            Require(!button.IsActivated && IsMarked(platform), "비활성 접촉 정리와 해제 부수효과 없음");
            p2.enabled = true;
            button.OnTriggerEnter2D(p2);
            UnityEngine.Object.DestroyImmediate(p2.gameObject);
            typeof(LayerBasedSwitch).GetMethod("FixedUpdate", Hidden).Invoke(button, null);
            Require(!button.IsActivated, "파괴된 접촉 정리");

            var clientButton = client.GetComponentInChildren<DetectPlatformResetBtn>(true);
            clientButton.OnTriggerEnter2D(p1);
            Require(!clientButton.IsActivated, "클라이언트 접촉 쓰기 차단");
            var door = server.GetComponentInChildren<DoorSwitch>(true);
            var remoteDoor = client.GetComponentInChildren<DoorSwitch>(true);
            door.OnStartServer();
            Require(door.door.GetComponent<Collider2D>().enabled, "서버 초기 문 충돌");
            door.OnTriggerEnter2D(p1);
            Require(!door.door.GetComponent<Collider2D>().enabled, "서버 문 열림");
            Transfer(door, remoteDoor, true);
            remoteDoor.OnStartClient();
            Require(!remoteDoor.door.GetComponent<Collider2D>().enabled && !remoteDoor.door.GetComponent<SpriteRenderer>().enabled, "초기 SyncVar 문 표시와 충돌");
            var p3 = Contact(server, "SecondCollider", "Player");
            door.OnTriggerEnter2D(p3);
            door.OnTriggerExit2D(p1);
            Require(!door.door.GetComponent<Collider2D>().enabled, "다중 접촉 문 유지");
            p3.gameObject.SetActive(false);
            typeof(LayerBasedSwitch).GetMethod("FixedUpdate", Hidden).Invoke(door, null);
            Require(door.door.GetComponent<Collider2D>().enabled, "비활성 대상 정리 후 문 닫힘");
            Transfer(door, remoteDoor, false);
            Require(remoteDoor.door.GetComponent<Collider2D>().enabled && remoteDoor.door.GetComponent<SpriteRenderer>().enabled, "변경 SyncVar 문 복원");
            Debug.Log("StageDoorResetChecks: PASS (유효 접촉 1회 서버 리셋, 중복/다중 콜라이더, 해제, 비대상 레이어, 파괴/비활성 접촉, 문 초기/변경 SyncVar). 실제 머리 누름 물리와 호스트/원격 플레이는 별도입니다.");
        }
        finally
        {
            active.SetValue(null, false);
            if (client != null) PrefabUtility.UnloadPrefabContents(client);
            if (server != null) PrefabUtility.UnloadPrefabContents(server);
        }
    }

    private static Collider2D Contact(GameObject root, string name, string layer)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(root.transform);
        obj.layer = LayerMask.NameToLayer(layer);
        return obj.AddComponent<BoxCollider2D>();
    }

    private static void Mark(PlayerDetectPlatform platform, bool value) => typeof(PlayerDetectPlatform).GetField("isPlayerDetected", Hidden).SetValue(platform, value);
    private static bool IsMarked(PlayerDetectPlatform platform) => (bool)typeof(PlayerDetectPlatform).GetField("isPlayerDetected", Hidden).GetValue(platform);
    private static void Initialize(GameObject root, bool isServer)
    {
        var identity = root.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Hidden).Invoke(identity, null);
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, isServer);
    }

    private static void Transfer(NetworkBehaviour source, NetworkBehaviour target, bool initial)
    {
        using (var writer = NetworkWriterPool.Get())
        {
            source.OnSerialize(writer, initial);
            using (var reader = NetworkReaderPool.Get(writer.ToArraySegment())) target.OnDeserialize(reader, initial);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("문/리셋 버튼 검사 실패: " + message);
    }
}
