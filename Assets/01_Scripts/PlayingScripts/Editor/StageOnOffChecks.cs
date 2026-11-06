using System;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;

public static class StageOnOffChecks
{
    [MenuItem("Tools/Flip Friends/Stages/Check OnOff State And Serialization")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || NetworkServer.active || NetworkClient.active)
            throw new InvalidOperationException("네트워크가 종료된 편집 모드에서만 검사합니다.");
        GameObject server = null;
        GameObject client = null;
        PropertyInfo activeProperty = typeof(NetworkServer).GetProperty("active");
        try
        {
            server = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 18.prefab");
            client = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 18.prefab");
            Initialize(server, true);
            Initialize(client, false);
            // 전송 계층 없이 실제 Weaver 코드의 서버 가드·직렬화·hook을 검사한다.
            activeProperty.SetValue(null, true);
            var manager = server.GetComponentInChildren<OnOffManager>(true);
            var switches = server.GetComponentsInChildren<OnOffSwitch>(true);
            foreach (var switchUnit in switches)
            {
                var serialized = new SerializedObject(switchUnit);
                serialized.FindProperty("toggleCooldown").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            manager.OnStartServer();
            AssertGroup(server, false);
            switches[0].ToggleSwitch();
            AssertGroup(server, true);
            switches[1].ToggleSwitch();
            AssertGroup(server, false);
            switches[0].ToggleSwitch();
            AssertGroup(server, true);
            Transfer(server, client, true);
            AssertGroup(client, true);

            manager.ToggleOnOffState(false);
            Transfer(server, client, false);
            AssertGroup(client, false);
            manager.ToggleState();
            switches[1].ToggleSwitch();
            AssertGroup(server, false);
            manager.ToggleOnOffState(false);
            AssertGroup(server, false);

            // 호스트 프로세스 안에서도 클라이언트 객체가 상태를 바꾸지 못해야 한다.
            client.GetComponentInChildren<OnOffManager>(true).ToggleState();
            client.GetComponentsInChildren<OnOffSwitch>(true)[0].ToggleSwitch();
            AssertGroup(client, false);
            foreach (Conveyor belt in server.GetComponentsInChildren<Conveyor>(true)) belt.OnStartServer();
            AssertGroup(server, false);
            CheckContacts(server);
            CheckIndependentGroups();
            CheckGrounds();
            Debug.Log("StageOnOffChecks: PASS (A/B/A, 자동 반전 후 누름, 멱등성, 초기/변경 SyncVar 전달, 클라이언트 쓰기 차단, 벨트 초기화 순서, 다중 접촉, 두 그룹 독립성, 지형 전환)");
        }
        finally
        {
            activeProperty.SetValue(null, false);
            if (client != null) PrefabUtility.UnloadPrefabContents(client);
            if (server != null) PrefabUtility.UnloadPrefabContents(server);
        }
    }

    private static void Initialize(GameObject root, bool isServer)
    {
        var identity = root.GetComponent<NetworkIdentity>();
        typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(identity, null);
        typeof(NetworkIdentity).GetProperty("isServer").SetValue(identity, isServer);
    }

    private static void Transfer(GameObject server, GameObject client, bool initial)
    {
        var sources = server.GetComponentsInChildren<NetworkBehaviour>(true);
        var targets = client.GetComponentsInChildren<NetworkBehaviour>(true);
        Require(sources.Length == targets.Length, "동일 컴포넌트 배열");
        for (int i = 0; i < sources.Length; i++)
        {
            if (!(sources[i] is OnOffManager) && !(sources[i] is OnOffSwitch) && !(sources[i] is Conveyor)) continue;
            using (var writer = NetworkWriterPool.Get())
            {
                sources[i].OnSerialize(writer, initial);
                using (var reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                    targets[i].OnDeserialize(reader, initial);
            }
            if (initial) targets[i].OnStartClient();
        }
    }

    private static void AssertGroup(GameObject root, bool expected)
    {
        Require(root.GetComponentInChildren<OnOffManager>(true).IsActivated == expected, "그룹 상태");
        foreach (var switchUnit in root.GetComponentsInChildren<OnOffSwitch>(true))
        {
            Require(switchUnit.IsActivated == expected, "개별 상태");
            Require(switchUnit.GetComponent<SpriteRenderer>().sprite == (expected ? switchUnit.onSwitchSprite : switchUnit.offSwitchSprite), "스위치 표시");
        }
        foreach (Conveyor belt in root.GetComponentsInChildren<Conveyor>(true))
        {
            Require(belt.isClockwise == expected, "벨트 방향");
            foreach (Animator animator in belt.GetComponentsInChildren<Animator>(true))
                Require(animator.GetBool("isClockwise") == expected, "벨트 애니메이션");
        }
    }

    private static void CheckContacts(GameObject root)
    {
        // 문 부수 효과를 피하고 동일한 부모 클래스의 실제 접촉 관리만 검사한다.
        var buttonObject = new GameObject("ContactCheck");
        buttonObject.transform.SetParent(root.transform);
        buttonObject.AddComponent<SpriteRenderer>();
        var button = buttonObject.AddComponent<LayerBasedSwitch>();
        typeof(NetworkBehaviour).GetProperty("netIdentity").SetValue(button, root.GetComponent<NetworkIdentity>());
        var serialized = new SerializedObject(button);
        serialized.FindProperty("activationLayer").intValue = 1 << LayerMask.NameToLayer("Player");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var first = new GameObject("FirstContact");
        var second = new GameObject("SecondContact");
        first.transform.SetParent(root.transform);
        second.transform.SetParent(root.transform);
        first.layer = second.layer = LayerMask.NameToLayer("Player");
        var a = first.AddComponent<BoxCollider2D>();
        var b = second.AddComponent<BoxCollider2D>();
        button.OnTriggerEnter2D(a);
        button.OnTriggerEnter2D(a);
        button.OnTriggerEnter2D(b);
        Require(button.IsActivated, "접촉 활성");
        button.OnTriggerExit2D(a);
        Require(button.IsActivated, "한 명 이탈 후 활성 유지");
        button.OnTriggerExit2D(b);
        Require(!button.IsActivated, "마지막 접촉 해제");
    }

    private static void CheckIndependentGroups()
    {
        var root = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 21.prefab");
        try
        {
            Initialize(root, true);
            var managers = root.GetComponentsInChildren<OnOffManager>(true);
            Require(managers.Length == 2, "독립 그룹 두 개");
            foreach (var manager in managers) manager.ToggleOnOffState(false);
            managers[0].ToggleState();
            managers[1].ToggleState();
            managers[0].ToggleState();
            foreach (var switchUnit in root.GetComponentsInChildren<OnOffSwitch>(true))
                Require(switchUnit.IsActivated == (switchUnit.Manager == managers[1]), "그룹 간 스위치 상태 격리");
            foreach (var manager in managers)
            {
                var belts = StageReferenceAudit.ReadList<Conveyor>(new SerializedObject(manager).FindProperty("conveyorBelts"));
                foreach (var belt in belts) Require(belt.isClockwise == (manager == managers[1]), "그룹 간 벨트 상태 격리");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CheckGrounds()
    {
        var root = PrefabUtility.LoadPrefabContents(StageReferenceAudit.StageFolder + "/Stage 11.prefab");
        try
        {
            Initialize(root, true);
            var manager = root.GetComponentInChildren<OnOffManager>(true);
            var serialized = new SerializedObject(manager);
            var on = (GameObject)serialized.FindProperty("activeSwitchGround").objectReferenceValue;
            var off = (GameObject)serialized.FindProperty("inactiveSwitchGround").objectReferenceValue;
            manager.ToggleOnOffState(false);
            Require(!on.activeSelf && off.activeSelf, "초기 지형");
            manager.ToggleState();
            Require(on.activeSelf && !off.activeSelf, "반전 지형");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("OnOff 검사 실패: " + label);
    }
}
