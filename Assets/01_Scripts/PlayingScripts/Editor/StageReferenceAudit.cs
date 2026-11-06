using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class StageReferenceAudit
{
    public const string StageFolder = "Assets/03_Prefabs/Stage";

    [Serializable]
    public sealed class Issue
    {
        public string severity;
        public string code;
        public string stage;
        public string objectPath;
        public string component;
        public string field;
        public string message;
    }

    [Serializable]
    public sealed class Report
    {
        public string generatedUtc = DateTime.UtcNow.ToString("O");
        public int stageCount;
        public List<Issue> issues = new List<Issue>();
    }

    [MenuItem("Tools/Flip Friends/Stages/Audit References")]
    public static void AuditMenu()
    {
        Report report = AuditAll();
        foreach (Issue issue in report.issues)
            Debug.Log($"[{issue.severity}/{issue.code}] {issue.objectPath} ({issue.component}.{issue.field}): {issue.message}");
        Debug.Log($"스테이지 {report.stageCount}개: 오류 {report.issues.Count(i => i.severity == "Error")}개, 확인 {report.issues.Count(i => i.severity == "Review")}개");
    }

    public static IEnumerable<string> StagePaths() => AssetDatabase.FindAssets("t:Prefab", new[] { StageFolder })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal);

    public static Report AuditAll()
    {
        var report = new Report();
        foreach (string path in StagePaths())
        {
            AuditRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path), report);
            report.stageCount++;
        }
        return report;
    }

    public static void AuditRoot(GameObject root, Report report)
    {
        NetworkIdentity[] identities = root.GetComponentsInChildren<NetworkIdentity>(true);
        if (identities.Length != 1 || root.GetComponent<NetworkIdentity>() == null)
            Add(report, root.transform, "NetworkIdentity", "", "IdentityLayout", "루트에 NetworkIdentity 하나만 있어야 합니다.");

        var managers = root.GetComponentsInChildren<OnOffManager>(true);
        var switches = root.GetComponentsInChildren<OnOffSwitch>(true);
        var belts = root.GetComponentsInChildren<Conveyor>(true);
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (Component component in child.GetComponents<Component>())
            {
                if (component == null)
                {
                    Add(report, child, "Missing Script", "m_Script", "MissingScript", "스크립트가 없습니다.");
                    continue;
                }
                if (!(component is MonoBehaviour) || component.GetType().Assembly != typeof(OnOffManager).Assembly)
                    continue;

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script")
                        continue;
                    UnityEngine.Object value = property.objectReferenceValue;
                    if (value == null)
                    {
                        if (IsRequired(component, property.propertyPath))
                            Add(report, child, component.GetType().Name, property.propertyPath, "RequiredReference", "필수 참조가 없습니다.");
                        else if (property.propertyPath.Contains(".Array.data["))
                            Add(report, child, component.GetType().Name, property.propertyPath, "NullListEntry", "목록 안의 빈 항목입니다.", "Review");
                        continue;
                    }
                    Transform reference = (value as Component)?.transform ?? (value as GameObject)?.transform;
                    if (reference != null && reference.root != root.transform &&
                        AssetDatabase.GetAssetPath(reference.gameObject) != AssetDatabase.GetAssetPath(root))
                    {
                        bool isPrefabTemplate = component is CannonShooter && property.name == "projectilePrefab";
                        if (!isPrefabTemplate)
                            Add(report, child, component.GetType().Name, property.propertyPath, "ExternalReference", "다른 프리팹/스테이지의 오브젝트를 참조합니다.");
                    }
                }

                if (component is Switch switchUnit && switchUnit.GetComponent<SpriteRenderer>() == null)
                    Add(report, child, component.GetType().Name, "SpriteRenderer", "MissingRenderer", "스위치 자신의 SpriteRenderer가 없습니다.");
                if (component is LayerBasedSwitch && serialized.FindProperty("activationLayer").intValue == 0)
                    Add(report, child, component.GetType().Name, "activationLayer", "EmptyActivationMask", "감지할 레이어가 없습니다.");
                if (component is OnOffSwitch onOff && onOff.collisionMask.value == 0)
                    Add(report, child, component.GetType().Name, "collisionMask", "EmptyDetectionMask", "스위치 감지 마스크가 비었습니다.");
                if (component is PlatformController platform)
                {
                    if (platform.localWaypoints == null || platform.localWaypoints.Length < 2)
                        Add(report, child, component.GetType().Name, "localWaypoints", "InvalidPath", "이동 경유점이 2개 미만입니다.");
                    if (platform.TransportsPassengers && platform.passengerMask.value == 0)
                        Add(report, child, component.GetType().Name, "passengerMask", "EmptyPassengerMask", "탑승/플레이어 감지 마스크가 비었습니다.", component is PlayerDetectPlatform ? "Error" : "Review");
                }
                if (component is PlayerDetectPlatform detect && detect.UsesSpriteEffects && detect.spriteRenderer == null)
                    Add(report, child, component.GetType().Name, "spriteRenderer", "OptionalRenderer", "활성화한 연출에 필요한 렌더러가 없습니다.");
                if (component is DoorSwitch door && door.door != null &&
                    (door.door.GetComponent<SpriteRenderer>() == null || door.door.GetComponent<Collider2D>() == null))
                    Add(report, child, component.GetType().Name, "door", "InvalidDoor", "문 대상에 렌더러/콜라이더가 없습니다.");
                if (component is ObjSummonSwitch summon && summon.targetObjTrans != null &&
                    summon.targetObjTrans.GetComponent<PickupObj>() == null)
                    Add(report, child, "ObjSummonSwitch", "targetObjTrans", "InvalidSummonTarget", "소환 대상에 PickupObj가 없습니다.");
                if (component is PickupObj && child.gameObject.layer != LayerMask.NameToLayer("Pickable"))
                    Add(report, child, "PickupObj", "layer", "InvalidBoxLayer", "상자는 Pickable 레이어여야 합니다.");
                if (component is RespawnHandler respawn && respawn.targetLayers.value == 0)
                    Add(report, child, "RespawnHandler", "targetLayers", "EmptyResetMask", "리셋 대상 레이어가 없습니다.");
                if (component is SavePoint checkpoint && checkpoint.savePointID < 0)
                    Add(report, child, "SavePoint", "savePointID", "InvalidCheckpointId", "체크포인트 ID가 음수입니다.");
                if (component is CannonShooter cannon && !cannon.TryValidateConfiguration(out string cannonProblem))
                    Add(report, child, "CannonShooter", "projectilePrefab", "InvalidCannon", cannonProblem);
                if (component is MovingTrap moving && !moving.TryValidateConfiguration(out string movingProblem))
                    Add(report, child, "MovingTrap", "endPosition", "InvalidTrapMotion", movingProblem);
                if (component is RotatingObstacle rotating && !rotating.TryValidateConfiguration(out string rotationProblem))
                    Add(report, child, "RotatingObstacle", "rotationSpeed", "InvalidRotation", rotationProblem);
                if (component is RotatingObstacle || component is DynamicObj)
                {
                    var transformSync = child.GetComponent<NetworkTransformBase>();
                    if (transformSync == null || transformSync.target != child || transformSync.syncDirection != SyncDirection.ServerToClient)
                        Add(report, child, component.GetType().Name, "NetworkTransform", "InvalidTransformSync", "자기 Transform을 대상으로 하는 서버 권한 위치/회전 동기화가 필요합니다.");
                }
            }
        }

        foreach (OnOffManager manager in managers)
        {
            var serialized = new SerializedObject(manager);
            var listed = ReadList<OnOffSwitch>(serialized.FindProperty("switches"));
            foreach (OnOffSwitch switchUnit in switches)
            {
                var owner = new SerializedObject(switchUnit).FindProperty("onOffManager").objectReferenceValue;
                if (owner == manager && !listed.Contains(switchUnit))
                    Add(report, switchUnit.transform, "OnOffManager", "switches", "UnlistedSwitch", $"{manager.name}의 목록에 빠졌습니다.");
            }
            foreach (OnOffSwitch switchUnit in listed.Where(s => s != null))
                if (new SerializedObject(switchUnit).FindProperty("onOffManager").objectReferenceValue != manager)
                    Add(report, manager.transform, "OnOffManager", "switches", "WrongSwitchOwner", $"{switchUnit.name}의 매니저와 목록 소유자가 다릅니다.");
            CheckDuplicates(report, manager, "switches", listed.Cast<UnityEngine.Object>());
            CheckDuplicates(report, manager, "conveyorBelts", ReadList<Conveyor>(serialized.FindProperty("conveyorBelts")).Cast<UnityEngine.Object>());
            foreach (string field in new[] { "activeSwitchGround", "inactiveSwitchGround" })
            {
                string expectedName = field == "activeSwitchGround" ? "OnSwitchGround" : "OffSwitchGround";
                if (serialized.FindProperty(field).objectReferenceValue == null && root.GetComponentsInChildren<Tilemap>(true)
                    .Any(t => t.name == expectedName && t.GetUsedTilesCount() > 0))
                    Add(report, manager.transform, "OnOffManager", field, "UnassignedGround", "타일이 있는 전환 지형의 명시적 참조가 없습니다.", "Review");
            }
        }
        foreach (Conveyor belt in belts)
        {
            int owners = managers.Count(m => ReadList<Conveyor>(new SerializedObject(m).FindProperty("conveyorBelts")).Contains(belt));
            if (owners != 1)
                Add(report, belt.transform, "Conveyor", "conveyorBelts", owners == 0 ? "UnassignedBelt" : "MultipleBeltOwners",
                    owners == 0 ? "고정 방향 또는 제어 매니저를 확인해야 합니다." : "여러 매니저가 같은 벨트를 제어합니다.", owners == 0 ? "Review" : "Error");
        }
    }

    public static List<T> ReadList<T>(SerializedProperty list) where T : UnityEngine.Object
    {
        var result = new List<T>();
        for (int i = 0; i < list.arraySize; i++)
            result.Add(list.GetArrayElementAtIndex(i).objectReferenceValue as T);
        return result;
    }

    private static bool IsRequired(Component component, string field)
    {
        if (component is Switch && (field == "onSwitchSprite" || field == "offSwitchSprite")) return true;
        if (component is OnOffSwitch && field == "onOffManager") return true;
        if (component is DetectPlatformResetBtn && field == "detectPlatform") return true;
        if (component is RespawnHandler && field == "resetPoint") return true;
        if (component is DoorSwitch && field == "door") return true;
        if (component is CannonShooter && field == "projectilePrefab") return true;
        return component is ObjSummonSwitch && (field == "summonLocation" || field == "targetObjTrans" || field == "defaultSprite" || field == "pressedSprite");
    }

    private static void CheckDuplicates(Report report, Component component, string field, IEnumerable<UnityEngine.Object> values)
    {
        if (values.Where(v => v != null).GroupBy(v => v).Any(g => g.Count() > 1))
            Add(report, component.transform, component.GetType().Name, field, "DuplicateReference", "같은 항목이 여러 번 연결됐습니다.");
    }

    private static void Add(Report report, Transform target, string component, string field, string code, string message, string severity = "Error")
    {
        string path = target.name;
        for (Transform parent = target.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        report.issues.Add(new Issue { severity = severity, code = code, stage = target.root.name, objectPath = path, component = component, field = field, message = message });
    }
}
