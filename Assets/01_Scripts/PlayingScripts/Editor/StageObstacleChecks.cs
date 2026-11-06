using System;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StageObstacleChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Flip Friends/Stages/Check Obstacles Projectiles And Player Counts")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active || NetworkServer.spawned.Count != 0)
            throw new InvalidOperationException("네트워크가 종료되고 스폰 목록이 빈 편집 모드에서 검사합니다.");
        GameObject movingRoot = null, rotatingRoot = null, remoteRoot = null, dynamicRoot = null, cannonRoot = null;
        GameObject projectile = null, remoteProjectile = null;
        var active = typeof(NetworkServer).GetProperty("active");
        var idCounter = typeof(NetworkIdentity).GetField("nextNetworkId", BindingFlags.Static | BindingFlags.NonPublic);
        object previousId = idCounter.GetValue(null);
        var randomState = UnityEngine.Random.state;
        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            movingRoot = LoadFirst<MovingTrap>();
            rotatingRoot = LoadFirst<RotatingObstacle>();
            remoteRoot = LoadFirst<RotatingObstacle>();
            dynamicRoot = LoadFirst<DynamicObj>();
            cannonRoot = LoadFirst<CannonShooter>();
            Initialize(movingRoot, true);
            Initialize(rotatingRoot, true);
            Initialize(remoteRoot, false);
            Initialize(dynamicRoot, true);
            Initialize(cannonRoot, true);
            active.SetValue(null, true);

            var moving = movingRoot.GetComponentInChildren<MovingTrap>(true);
            Invoke(moving, "Awake");
            Vector2 start = moving.transform.position;
            Set(moving, "endPosition", new Vector2(8, 6));
            Set(moving, "speed", 2f);
            Set(moving, "rotationSpeed", 90f);
            moving.OnStartServer();
            Set(moving, "startTime", 42d);
            moving.OnStartServer();
            Require(Get<double>(moving, "startTime") == 42, "이동 트랩 반복 초기화 위상 유지");
            Require(Near(Evaluate(moving, -1), start), "시작 전 이동 없음");
            Require(Near(Evaluate(moving, 2.5), start + new Vector2(4, 3)), "이동 속도와 경로");
            Require(Near(Evaluate(moving, 5), start + new Vector2(8, 6)), "이동 끝점");
            Require(Near(Evaluate(moving, 7.5), start + new Vector2(4, 3)) && Near(Evaluate(moving, 10), start), "왕복 복귀");
            Set(moving, "speed", 0f);
            Require(Near(Evaluate(moving, 10), start), "정지 트랩");
            float angle = (float)Invoke(moving, "EvaluateAngle", 1000000d);
            Require(!float.IsNaN(angle) && !float.IsInfinity(angle), "장시간 회전 정밀도");

            var rotating = rotatingRoot.GetComponentInChildren<RotatingObstacle>(true);
            var remoteRotation = remoteRoot.GetComponentInChildren<RotatingObstacle>(true);
            rotating.random = true;
            rotating.rotationSpeed = 120;
            rotating.randomRange = 10;
            UnityEngine.Random.InitState(4731);
            rotating.OnStartServer();
            float selected = rotating.SelectedRotationSpeed;
            bool direction = rotating.SelectedCounterclockwise;
            rotating.OnStartServer();
            Require(selected >= 110 && selected <= 130 && rotating.SelectedRotationSpeed == selected && rotating.SelectedCounterclockwise == direction, "서버 랜덤 선택 1회");
            Require(rotating.rotationSpeed == 120, "회전 설정값 보존");
            Require(Mathf.Abs(rotating.RotationDelta(.5f) - (direction ? -1 : 1) * selected * .5f) < .001f, "회전 방향과 속도");
            var beforeRemoteRandom = UnityEngine.Random.state;
            remoteRotation.OnStartClient();
            float remoteRandom = UnityEngine.Random.value;
            UnityEngine.Random.state = beforeRemoteRandom;
            Require(UnityEngine.Random.value == remoteRandom, "클라이언트 랜덤 재선택 없음");
            Transfer(rotating, remoteRotation, true);
            Require(remoteRotation.SelectedRotationSpeed == selected && remoteRotation.SelectedCounterclockwise == direction, "랜덤 확정값 직렬화");
            Quaternion previousRotation = remoteRotation.transform.rotation;
            Invoke(remoteRotation, "Update");
            Require(previousRotation == remoteRotation.transform.rotation, "클라이언트 자체 회전 차단");

            var dynamic = dynamicRoot.GetComponentInChildren<DynamicObj>(true);
            Vector3 origin = dynamic.transform.position;
            for (int players = 1; players <= 4; players++)
            {
                dynamic.ServerApplyPlayerCount(players);
                dynamic.ServerApplyPlayerCount(players);
                Require(dynamic.transform.position == origin + new Vector3(dynamic.xMoveValue, dynamic.yMoveValue, 0) * (players - 1), "인원별 배치 멱등성 " + players);
            }
            dynamic.ServerApplyPlayerCount(0);
            Require(dynamic.transform.position == origin, "플레이어 0명일 때 음수 오프셋 없음");

            var cannon = cannonRoot.GetComponentInChildren<CannonShooter>(true);
            Set(cannon, "firstShootDelay", .75f);
            Set(cannon, "shootInterval", 1.25f);
            Set(cannon, "projectileSpeed", 7f);
            Set(cannon, "maxProjectileDistance", 12f);
            Set(cannon, "shootDirection", new Vector2(0, -2));
            double now = NetworkTime.time;
            cannon.OnStartServer();
            Require(cannon.IsScheduled && cannon.NextShootTime >= now + .75 && cannon.NextShootTime <= NetworkTime.time + .75, "첫 발사 지연");
            double scheduled = cannon.NextShootTime;
            cannon.OnStartServer();
            Require(cannon.NextShootTime == scheduled, "중복 발사 예약 방지");
            Invoke(cannon, "AdvanceSchedule", scheduled + .01);
            Require(Math.Abs(cannon.NextShootTime - scheduled - 1.25) < .001, "발사 주기");
            Invoke(cannon, "AdvanceSchedule", scheduled + 10);
            Require(Math.Abs(cannon.NextShootTime - scheduled - 11.25) < .001, "지연 후 연속 폭주 방지");
            projectile = (GameObject)Invoke(cannon, "CreateProjectile");
            SceneManager.MoveGameObjectToScene(projectile, scene);
            var flight = projectile.GetComponent<CannonProjectile>();
            Require(projectile.GetComponent<NetworkIdentity>().netId == 0 && projectile.GetComponent<Rigidbody2D>().linearVelocity == new Vector2(0, -7), "Spawn 전 서버 발사 속도 초기화");
            Require(Vector3.Distance(projectile.transform.position, cannon.transform.position + Vector3.down) < .001, "발사 위치/방향 보존");
            Initialize(projectile, false);
            NetworkServer.Spawn(projectile);
            Require(projectile.GetComponent<NetworkIdentity>().isServer && NetworkServer.spawned.ContainsKey(projectile.GetComponent<NetworkIdentity>().netId), "실제 Mirror Spawn 등록");
            Vector3 launch = projectile.transform.position;
            projectile.transform.position = launch + Vector3.right * 12;
            Require(!flight.IsOutOfRange, "거리 경계에서는 유지");
            projectile.transform.position += Vector3.right * .1f;
            Require(flight.IsOutOfRange, "범위를 넘으면 제거 대상");
            remoteProjectile = UnityEngine.Object.Instantiate(cannon.ProjectilePrefab);
            SceneManager.MoveGameObjectToScene(remoteProjectile, scene);
            Initialize(remoteProjectile, false);
            var remoteFlight = remoteProjectile.GetComponent<CannonProjectile>();
            Transfer(flight, remoteFlight, true);
            remoteFlight.OnStartClient();
            Require(!remoteProjectile.GetComponent<Rigidbody2D>().simulated && remoteFlight.LaunchVelocity == new Vector2(0, -7), "원격 탄환은 서버 상태를 받고 물리를 중복 실행하지 않음");
            Invoke(cannon, "OnDisable");
            Require(!cannon.IsScheduled && projectile.activeInHierarchy, "대포 비활성화 시 예약 해제와 탄환 수명 독립");
            Invoke(cannon, "OnEnable");
            Require(cannon.IsScheduled, "재활성화 예약 복구");
            cannon.OnStopServer();
            Require(!cannon.IsScheduled, "서버 중지 예약 해제");
            NetworkServer.UnSpawn(projectile);
            Require(NetworkServer.spawned.Count == 0, "Mirror 스폰 정리");
            Debug.Log("StageObstacleChecks: PASS (트랩 왕복/정지/위상, 서버 랜덤 회전/직렬화, 1~4인 배치, 발사 지연/주기/예약, Spawn 전 초기화, 실제 Mirror Spawn/UnSpawn, 거리 경계, 원격 물리 차단). 소켓과 실제 클리어는 별도입니다.");
        }
        finally
        {
            if (projectile != null && projectile.GetComponent<NetworkIdentity>().netId != 0 && NetworkServer.active)
                NetworkServer.UnSpawn(projectile);
            active.SetValue(null, false);
            idCounter.SetValue(null, previousId);
            UnityEngine.Random.state = randomState;
            if (projectile != null) UnityEngine.Object.DestroyImmediate(projectile);
            if (remoteProjectile != null) UnityEngine.Object.DestroyImmediate(remoteProjectile);
            foreach (var root in new[] { movingRoot, rotatingRoot, remoteRoot, dynamicRoot, cannonRoot })
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
        }
    }

    private static GameObject LoadFirst<T>() where T : Component
    {
        foreach (var path in StageReferenceAudit.StagePaths())
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<T>(true) != null)
                return PrefabUtility.LoadPrefabContents(path);
        throw new InvalidOperationException(typeof(T).Name + " 스테이지가 없습니다.");
    }
    private static Vector2 Evaluate(MovingTrap trap, double elapsed) => (Vector2)Invoke(trap, "EvaluatePosition", elapsed);
    private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .001f;
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
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
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("장애물 검사 실패: " + name);
    }
}
