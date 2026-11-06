using System;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StagePlatformChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Flip Friends/Stages/Check Platform Motion And Prediction")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
            throw new InvalidOperationException("네트워크가 종료된 편집 모드에서만 검사합니다.");
        CheckPath();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = default;
        NetworkIdentity registeredIdentity = null;
        var active = typeof(NetworkServer).GetProperty("active");
        try
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var platformObject = new GameObject("PlatformCheck");
            platformObject.transform.position = new Vector3(-500, -500, 0);
            platformObject.layer = LayerMask.NameToLayer("Ground");
            platformObject.tag = "Ground";
            platformObject.AddComponent<NetworkIdentity>();
            platformObject.AddComponent<SpriteRenderer>();
            var platform = platformObject.AddComponent<PlayerDetectPlatform>();
            platform.localWaypoints = new[] { Vector3.zero, new Vector3(2, 0, 0) };
            platform.speed = 1;
            platform.originalPositionDelay = 1;
            platform.passengerMask = LayerMask.GetMask("Player");
            platform.boxCollider = platformObject.GetComponent<BoxCollider2D>();
            platform.boxCollider.size = new Vector2(8, 1);
            platform.spriteRenderer = platformObject.GetComponent<SpriteRenderer>();
            Initialize(platformObject);
            registeredIdentity = platformObject.GetComponent<NetworkIdentity>();
            Require(!NetworkServer.spawned.ContainsKey(uint.MaxValue - 2), "검사용 ID 미사용");
            typeof(NetworkIdentity).GetProperty("netId").SetValue(registeredIdentity, uint.MaxValue - 2);
            NetworkServer.spawned.Add(uint.MaxValue - 2, registeredIdentity);
            active.SetValue(null, true);
            platform.Awake();
            platform.Start();
            platform.OnStartServer();
            platform.ServerActivate();
            SetBase(platform, "motionStartedAt", 10d);
            var initial = platform.InitialPosition;
            Require(Near(platform.PositionAt(10.5), initial + Vector3.right), "구간 진행률 속도와 easing");
            Require(platform.Displacement(10, 10.1, platform.MotionRevision).x > .1f, "시각별 탑승 이동량");
            Vector3 shown = platform.transform.position;
            try
            {
                using (PlatformController.AtSimulationTime(10.5))
                {
                    Require(Near(platform.transform.position, initial + Vector3.right), "입력 시점 발판 판정");
                    throw new ExpectedScopeException();
                }
            }
            catch (ExpectedScopeException) { }
            Require(platform.transform.position == shown, "예외 후 발판 위치 복구");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03_Prefabs/Player.prefab");
            var first = PreparePlayer(prefab, scene, platform, 0);
            var second = PreparePlayer(prefab, scene, platform, 1.5f);
            var firstInitial = first.GetState();
            var secondInitial = second.GetState();
            Tick(first, 10.1);
            Tick(second, 10.1);
            Require(Mathf.Abs(first.transform.position.x - firstInitial.position.x - .2f) < .04f, "첫 승객 이동");
            Require(Mathf.Abs(second.transform.position.x - secondInitial.position.x - .2f) < .04f, "두 번째 승객 독립 이동");
            Require(first.GetState().supportPlatform == platform, "탑승 접촉 유지: " + JsonUtility.ToJson(first.GetState()) + " layer=" + first.gameObject.layer + " mask=" + first.GetComponent<Controller2D>().collisionMask.value);
            Tick(first, 10.2);
            var predicted = first.GetState();
            first.SetState(firstInitial);
            Tick(first, 10.1);
            Tick(first, 10.2);
            Require(Vector2.Distance(first.GetState().position, predicted.position) < .001f, "재시뮬레이션 결과 일치");

            var wall = new GameObject("CarryWallCheck");
            wall.layer = LayerMask.NameToLayer("Ground");
            wall.transform.position = firstInitial.position + new Vector2(.75f, 0);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 4);
            first.SetState(firstInitial);
            Tick(first, 10.1);
            Physics2D.SyncTransforms();
            Require(first.GetComponent<Controller2D>().CurrentBounds.max.x <= wall.GetComponent<Collider2D>().bounds.min.x + .025f, "벽 옆 운반 시 관통 차단");
            UnityEngine.Object.DestroyImmediate(wall);

            StatePayload jump = firstInitial;
            jump.velocity = new Vector2(0, 5);
            first.SetState(jump);
            Tick(first, 10.1);
            Require(Mathf.Abs(first.transform.position.x - jump.position.x) < .02f && first.GetState().supportPlatform == null, "점프 시 발판에서 분리");

            platform.localWaypoints = new[] { Vector3.zero, new Vector3(0, 2, 0) };
            first.SetState(firstInitial);
            Tick(first, 10.1);
            Require(Mathf.Abs(first.transform.position.y - firstInitial.position.y - .2f) < .04f, "상승 발판 중복 밀기 없음");
            platform.localWaypoints = new[] { Vector3.zero, new Vector3(0, -2, 0) };
            first.SetState(firstInitial);
            Tick(first, 10.1);
            Require(Mathf.Abs(first.transform.position.y - firstInitial.position.y + .2f) < .04f, "하강 발판 접지 유지");
            platform.localWaypoints = new[] { Vector3.zero, new Vector3(2, 0, 0) };
            var remoteObject = UnityEngine.Object.Instantiate(platformObject);
            Initialize(remoteObject, false);
            var remote = remoteObject.GetComponent<PlayerDetectPlatform>();
            remote.Awake();
            Transfer(platform, remote, true);
            remote.OnStartClient();
            Require(Near(remote.PositionAt(10.5), platform.PositionAt(10.5)), "초기 발판 시계 직렬화");

            uint revision = platform.MotionRevision;
            platform.transform.position += Vector3.one * 20;
            platform.ResetPlatformState();
            Transfer(platform, remote, false);
            Require(Near(remote.PositionAt(10.5), initial) && !remote.IsMotionActive && remote.MotionRevision == platform.MotionRevision, "리셋 상태 원격 전달");
            Require(platform.transform.position == initial && !platform.IsMotionActive && platform.MotionRevision == revision + 1, "이동 중 리셋 위치/시계/세대 초기화");
            Require(platform.CurrentPhase == PlayerDetectPlatform.Phase.WaitingForPlayer, "감지 대기 복원");
            Require(platform.Displacement(10, 10.5, revision) == Vector2.zero, "리셋 전 탑승 상태 재사용 차단");
            platform.ResetPlatformState();
            Require(platform.transform.position == initial, "반복 리셋 위치 불변");
            platform.ServerActivate();
            SetBase(platform, "motionStartedAt", 10d);
            platform.disableColliderWhenFalling = true;
            Require(platform.IsCollidableAt(10.5) && !platform.IsCollidableAt(11.5) && platform.IsCollidableAt(12.1), "완료/복귀 시각별 충돌");
            SetBase(platform, "motionStartedAt", NetworkTime.time - 3);
            revision = platform.MotionRevision;
            platform.FixedUpdate();
            Require(platform.transform.position == initial && platform.CurrentPhase == PlayerDetectPlatform.Phase.WaitingForPlayer && platform.MotionRevision == revision + 1, "자동 복귀 후 초기 상태 복원");
            Require(platform.GetComponent<BoxCollider2D>().enabled && platform.spriteRenderer.color.a == 1, "복귀 후 판정과 알파 복원");
            uint remoteRevision = remote.MotionRevision;
            remote.ResetPlatformState();
            Require(remote.MotionRevision == remoteRevision, "클라이언트 발판 리셋 쓰기 차단");
            CheckStateSerialization(firstInitial);
            Debug.Log("StagePlatformChecks: PASS (경로/왕복/반복/대기/완료/복귀/속도 0, 이동 중 반복 리셋, 시점 복구, 2인 승객/재시뮬레이션/점프/상하 이동, 상태 직렬화). 소켓과 PlayMode 조작 검증은 별도입니다.");
        }
        finally
        {
            active.SetValue(null, false);
            if (registeredIdentity != null && NetworkServer.spawned.TryGetValue(uint.MaxValue - 2, out var value) && value == registeredIdentity)
                NetworkServer.spawned.Remove(uint.MaxValue - 2);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static MovementHandler PreparePlayer(GameObject prefab, Scene scene, PlayerDetectPlatform platform, float x)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Initialize(root);
        var controller = root.GetComponent<Controller2D>();
        controller.Awake();
        controller.Start();
        var movement = root.GetComponent<MovementHandler>();
        typeof(MovementHandler).GetMethod("Awake", Hidden).Invoke(movement, null);
        root.transform.position = platform.InitialPosition + new Vector3(x, 2, 0);
        Physics2D.SyncTransforms();
        var body = root.GetComponent<BoxCollider2D>();
        root.transform.position += Vector3.up * (platform.GetComponent<BoxCollider2D>().bounds.max.y - body.bounds.min.y);
        var state = movement.GetState();
        state.position = root.transform.position;
        state.isGrounded = true;
        state.supportPlatform = platform;
        state.supportRevision = platform.MotionRevision;
        state.platformTime = 10;
        movement.SetState(state);
        Physics2D.SyncTransforms();
        return movement;
    }

    private static void Tick(MovementHandler player, double time)
    {
        player.ApplyInput(new InputPayload { platformTime = time, deltaTime = Time.fixedDeltaTime });
        player.Simulate(Time.fixedDeltaTime);
    }

    private static void CheckPath()
    {
        CheckLegacyTicks();
        var points = new[] { Vector3.zero, new Vector3(10, 0, 0), new Vector3(10, 10, 0) };
        Require(Near(PlatformMotion.Evaluate(points, 2, .5f, false, false, 1, .25).offset, new Vector3(5, 0, 0)), "구간 진행률/초");
        Require(Near(PlatformMotion.Evaluate(points, 2, .5f, false, false, 1, .75).offset, new Vector3(10, 0, 0)), "경유점 대기");
        Require(PlatformMotion.Evaluate(points, 2, .5f, false, false, 1, 1.6).isCompleted, "완주");
        Require(PlatformMotion.Evaluate(points, 2, .5f, false, false, 1, 2.6).hasReturned, "지연 복귀");
        var reverse = PlatformMotion.Evaluate(points, 2, 0, true, false, 1, 1.25).offset;
        Require(reverse.x == 10 && reverse.y > 0 && reverse.y < 10, "경로 역방향 왕복");
        Require(PlatformMotion.Evaluate(points, 2, 0, false, true, 1, 1.25).cycle == 1, "반복 경로 구분");
        Require(PlatformMotion.Evaluate(points, 0, 0, false, false, 0, 100).offset == Vector3.zero, "속도 0 보존");
        Require(PlatformMotion.Evaluate(points, 2, 0, false, false, 0, -.1).offset == Vector3.zero, "출발 지연");
    }

    private static void CheckLegacyTicks()
    {
        var points = new[] { Vector3.zero, new Vector3(8, 0, 0), new Vector3(8, 8, 0) };
        foreach (float speed in new[] { .3f, 2f, 4f, 8f })
        {
            int leg = 0;
            float progress = 0;
            for (int tick = 1; tick <= 500; tick++)
            {
                progress = Mathf.Clamp01(progress + Time.fixedDeltaTime * speed);
                Vector3 legacy = Vector3.Lerp(points[leg], points[leg + 1], progress);
                var sample = PlatformMotion.Evaluate(points, speed, 0, false, true, 0, tick * (double)Time.fixedDeltaTime);
                Require(Vector3.Distance(legacy, sample.offset) < .002f, "기존 틱별 경로/속도 보존: " + speed + " tick=" + tick);
                if (progress >= 1)
                {
                    progress = 0;
                    leg = (leg + 1) % (points.Length - 1);
                }
            }
        }
    }

    private static void CheckStateSerialization(StatePayload value)
    {
        using (var writer = NetworkWriterPool.Get())
        {
            writer.Write(value);
            using (var reader = NetworkReaderPool.Get(writer.ToArraySegment()))
            {
                var result = reader.Read<StatePayload>();
                Require(result.platformTime == value.platformTime && result.supportRevision == value.supportRevision && result.supportPlatform == value.supportPlatform, "탑승 참조/시각/세대 상태 직렬화");
            }
        }
    }

    private static void Transfer(NetworkBehaviour source, NetworkBehaviour target, bool initial)
    {
        using (var writer = NetworkWriterPool.Get())
        {
            source.OnSerialize(writer, initial);
            using (var reader = NetworkReaderPool.Get(writer.ToArraySegment())) target.OnDeserialize(reader, initial);
        }
    }

    private static void Initialize(GameObject root, bool isServer = true)
    {
        var identity = root.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", Hidden).Invoke(identity, null);
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, isServer);
        typeof(NetworkIdentity).GetProperty("isClient").SetValue(identity, !isServer);
    }
    private static void SetBase(object target, string name, object value) => typeof(PlatformController).GetField(name, Hidden).SetValue(target, value);
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("발판 검사 실패: " + name);
    }
    private sealed class ExpectedScopeException : Exception { }
}
