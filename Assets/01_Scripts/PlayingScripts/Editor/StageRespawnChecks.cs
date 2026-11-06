using System;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;

public static class StageRespawnChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Flip Friends/Stages/Check Respawn State And Serialization")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
            throw new InvalidOperationException("네트워크가 종료된 편집 모드에서만 검사합니다.");
        GameObject server = null, client = null, playerRoot = null, managerObject = null;
        StageManager previousManager = StageManager.instance;
        var active = typeof(NetworkServer).GetProperty("active");
        try
        {
            server = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 10.prefab");
            client = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 10.prefab");
            playerRoot = PrefabUtility.LoadPrefabContents("Assets/03_Prefabs/Player.prefab");
            Initialize(server, true);
            Initialize(client, false);
            Initialize(playerRoot, true);
            Require(!NetworkServer.spawned.ContainsKey(uint.MaxValue) && !NetworkServer.spawned.ContainsKey(uint.MaxValue - 1), "검사용 ID 미사용");
            typeof(NetworkIdentity).GetProperty("netId").SetValue(server.GetComponent<NetworkIdentity>(), uint.MaxValue);
            typeof(NetworkIdentity).GetProperty("netId").SetValue(playerRoot.GetComponent<NetworkIdentity>(), uint.MaxValue - 1);
            NetworkServer.spawned.Add(uint.MaxValue, server.GetComponent<NetworkIdentity>());
            NetworkServer.spawned.Add(uint.MaxValue - 1, playerRoot.GetComponent<NetworkIdentity>());
            active.SetValue(null, true);
            var box = server.GetComponentInChildren<PickupObj>(true);
            var clientBox = client.GetComponentInChildren<PickupObj>(true);
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteNetworkBehaviour(box);
                using (var reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                    Require(reader.ReadNetworkBehaviour() == box, "루트 Identity의 자식 상자 참조 직렬화");
            }
            box.Awake();
            clientBox.Awake();
            box.Start();
            box.OnStartServer();
            var home = box.transform.position;
            box.Release(new Vector2(20, 30), new Vector2(5, -8));
            Require(Get<Vector2>(box, "velocity") == new Vector2(5, -8), "Release 발사 속도 유지");
            box.collisions = new Controller2D.CollisionInfo { above = true, isSlide = true, slopeAngle = 35, faceDir = -1 };
            box.ServerReset(new Vector2(21, 31));
            CheckBox(box, new Vector2(21, 31));
            Transfer(box, clientBox, true);
            clientBox.OnStartClient();
            Require((Vector2)clientBox.transform.position == new Vector2(21, 31), "초기 위치 전달");
            box.ServerReset(new Vector2(24, 34));
            Transfer(box, clientBox, false);
            Invoke(clientBox, "FollowSyncedPosition");
            Require((Vector2)clientBox.transform.position == new Vector2(24, 34), "재소환 SyncVar와 클라이언트 위치 일치");
            clientBox.ServerReset(Vector2.zero);
            Require((Vector2)clientBox.transform.position == new Vector2(24, 34), "클라이언트 상자 리셋 차단");

            var player = playerRoot.GetComponent<PlayerController2D>();
            var interaction = playerRoot.GetComponent<PlayerInteraction>();
            Invoke(player, "InitializeComponents");
            Invoke(interaction, "Awake");
            Invoke(playerRoot.GetComponent<MovementHandler>(), "Awake");
            Invoke(playerRoot.GetComponent<ServerMover>(), "Awake");
            playerRoot.GetComponent<Controller2D>().Awake();
            playerRoot.GetComponent<Controller2D>().Start();
            player.OnStartServer();
            var spawn = playerRoot.transform.position;
            Invoke(interaction, "PickUpObj", box);
            Require(interaction.IsHoldingObject && box.IsCarried, "운반 시작");
            Transfer(box, clientBox, false);
            Require(!clientBox.GetComponent<Collider2D>().enabled, "운반 상태 hook");
            box.ServerReset(new Vector2(25, 35));
            CheckBox(box, new Vector2(25, 35));
            Require(!interaction.IsHoldingObject && !interaction.catchedCollider.enabled, "재소환 시 운반자 참조와 표시 해제");
            Require(!Physics2D.GetIgnoreCollision(playerRoot.GetComponent<Collider2D>(), box.GetComponent<Collider2D>()), "충돌 무시 해제");
            Invoke(interaction, "FollowToPlayer");
            CheckBox(box, new Vector2(25, 35));
            Transfer(box, clientBox, false);
            Require(clientBox.GetComponent<Collider2D>().enabled, "운반 해제 hook");

            var reset = server.GetComponentInChildren<RespawnHandler>(true);
            var before = playerRoot.transform.position;
            player.ServerResetSection(reset.resetPoint.position, true);
            Require(playerRoot.transform.position == before, "onlyBoxReset 빈손 통과");
            Invoke(interaction, "PickUpObj", box);
            var mover = playerRoot.GetComponent<ServerMover>();
            ushort epoch = Get<ushort>(mover, "epoch");
            Invoke(reset, "ResetTarget", playerRoot);
            Require(playerRoot.transform.position == reset.resetPoint.position && interaction.IsHoldingObject, "구간 리셋 목적지와 운반 유지");
            Require(Get<ushort>(mover, "epoch") == epoch + 1, "Teleport 에포크 변경");
            Invoke(reset, "ResetTarget", playerRoot);
            Require(Get<ushort>(mover, "epoch") == epoch + 1, "동일 물리 스텝 중복 접촉 차단");
            Set(player, "carrier", interaction);
            player.ServerResetSection(Vector2.zero, false);
            Invoke(player, "ServerRespawn");
            Require(Get<ushort>(mover, "epoch") == epoch + 1, "들린 플레이어는 독립 리셋하지 않음");
            Set(player, "carrier", null);
            Invoke(player, "ServerRespawn");
            Require(playerRoot.transform.position == spawn && !interaction.IsHoldingObject, "리셋 키 공통 경로 시작점 복귀와 내려놓기");

            var low = server.AddComponent<SavePoint>();
            var highObject = new GameObject("CheckpointCheck");
            highObject.transform.SetParent(server.transform);
            highObject.transform.position = new Vector3(9, 12, 0);
            var high = highObject.AddComponent<SavePoint>();
            low.savePointID = 0;
            high.savePointID = 2;
            player.SetSavePoint(high);
            player.SetSavePoint(low);
            Require(Get<SavePoint>(player, "savePoint") == high, "체크포인트 역행 방지");
            low.savePointID = 2;
            player.SetSavePoint(low);
            Require(Get<SavePoint>(player, "savePoint") == high, "같은 ID는 최초 도달 위치 유지");
            Invoke(player, "ServerRespawn");
            Require(playerRoot.transform.position == high.transform.position, "체크포인트 리스폰");

            managerObject = new GameObject("FallBoundaryCheck");
            managerObject.AddComponent<NetworkIdentity>();
            var manager = managerObject.AddComponent<StageManager>();
            StageManager.instance = manager;
            typeof(StageManager).GetProperty("FallBoundary").SetValue(manager, new FallBoundary(new Bounds(Vector3.zero, new Vector3(100, 100, 0))));
            box.Release(new Vector2(0, -1000), new Vector2(4, -12));
            Invoke(box, "ReturnHomeIfOutsideStage");
            CheckBox(box, home);
            Invoke(interaction, "PickUpObj", box);
            playerRoot.transform.position = new Vector3(0, -1000, 0);
            Invoke(player, "RespawnIfOutsideStage");
            Require(playerRoot.transform.position == high.transform.position && !interaction.IsHoldingObject, "운반 중 추락은 체크포인트 복귀와 내려놓기");

            var summon = server.GetComponentInChildren<ObjSummonSwitch>(true);
            var clientSummon = client.GetComponentInChildren<ObjSummonSwitch>(true);
            Invoke(summon, "Awake");
            Invoke(clientSummon, "Awake");
            summon.OnStartServer();
            Require(summon.ServerTrySummon(), "재소환 실행");
            CheckBox(box, summon.summonLocation.position);
            Require(!summon.ServerTrySummon(), "쿨다운 중 중복 누름 차단");
            Transfer(summon, clientSummon, true);
            clientSummon.OnStartClient();
            Require(clientSummon.IsCooldown && clientSummon.GetComponent<SpriteRenderer>().sprite == clientSummon.pressedSprite, "원격 쿨다운 초기 표시");
            Set(summon, "cooldownEndsAt", NetworkTime.time - 1);
            Invoke(summon, "Update");
            Require(!summon.IsCooldown && summon.ServerTrySummon(), "쿨다운 종료와 재실행");
            Set(summon, "cooldownEndsAt", NetworkTime.time - 1);
            Invoke(summon, "Update");
            Transfer(summon, clientSummon, false);
            Require(!clientSummon.IsCooldown && clientSummon.GetComponent<SpriteRenderer>().sprite == clientSummon.defaultSprite, "원격 쿨다운 해제 hook");
            Debug.Log("StageRespawnChecks: PASS (상자 위치/속도/충돌/운반 해제, SyncVar, 구간/리셋키/추락 복귀, 중복 접촉, 체크포인트 역행, 소환 쿨다운). 실제 물리 접촉과 호스트/원격 소켓 검증은 별도입니다.");
        }
        finally
        {
            active.SetValue(null, false);
            if (server != null && NetworkServer.spawned.TryGetValue(uint.MaxValue, out var registeredStage) && registeredStage == server.GetComponent<NetworkIdentity>())
                NetworkServer.spawned.Remove(uint.MaxValue);
            if (playerRoot != null && NetworkServer.spawned.TryGetValue(uint.MaxValue - 1, out var registeredPlayer) && registeredPlayer == playerRoot.GetComponent<NetworkIdentity>())
                NetworkServer.spawned.Remove(uint.MaxValue - 1);
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            StageManager.instance = previousManager;
            if (playerRoot != null) PrefabUtility.UnloadPrefabContents(playerRoot);
            if (client != null) PrefabUtility.UnloadPrefabContents(client);
            if (server != null) PrefabUtility.UnloadPrefabContents(server);
        }
    }

    private static void CheckBox(PickupObj box, Vector2 position)
    {
        Require((Vector2)box.transform.position == position && Get<Vector2>(box, "syncedPosition") == position, "상자 위치와 동기화 위치");
        Require(Get<Vector2>(box, "velocity") == Vector2.zero && Get<float>(box, "velocityXSmoothing") == 0, "속도 초기화");
        Require(!box.IsCarried && !box.collisions.above && !box.collisions.isSlide && box.collisions.slopeAngle == 0, "운반/충돌 초기화");
        Require(box.GetComponent<Collider2D>().enabled && box.GetComponent<SpriteRenderer>().enabled, "상자 표시와 판정 복원");
    }

    private static void Initialize(GameObject root, bool isServer)
    {
        var identity = root.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", PrivateInstance).Invoke(identity, null);
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

    private static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, args);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("리스폰 검사 실패: " + message);
    }
}
