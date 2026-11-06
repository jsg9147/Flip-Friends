using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class StageOnOffRepair
{
    [MenuItem("Tools/Flip Friends/Stages/Preview Safe OnOff Repairs")]
    private static void PreviewMenu() => Debug.Log(string.Join("\n", RepairAll(false)));

    [MenuItem("Tools/Flip Friends/Stages/Apply Safe OnOff Repairs")]
    private static void RepairMenu() => Debug.Log(string.Join("\n", RepairAll(true)));

    public static List<string> RepairAll(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이 모드에서는 프리팹을 복구할 수 없습니다.");
        var openStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (apply && openStage != null && openStage.scene.isDirty && StageReferenceAudit.StagePaths().Contains(openStage.assetPath))
            throw new InvalidOperationException("저장되지 않은 프리팹 편집 내용이 있습니다: " + openStage.assetPath);
        var changes = new List<string>();
        foreach (string path in StageReferenceAudit.StagePaths())
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty)
                throw new InvalidOperationException("저장되지 않은 프리팹 편집 내용이 있습니다: " + path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = RepairRoot(root, apply, changes);
                if (apply && count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    if (!success) throw new InvalidOperationException("프리팹 저장 실패: " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return changes;
    }

    public static int RepairRoot(GameObject root, bool apply, List<string> changes)
    {
        int initialCount = changes.Count;
        var managers = root.GetComponentsInChildren<OnOffManager>(true);
        var switches = root.GetComponentsInChildren<OnOffSwitch>(true);
        foreach (var manager in managers)
        {
            var serialized = new SerializedObject(manager);
            var owned = switches.Where(s => new SerializedObject(s).FindProperty("onOffManager").objectReferenceValue == manager).ToArray();
            // 역참조가 명시된 스위치만 복구하고 이름이나 거리로 소유자를 추측하지 않는다.
            var list = serialized.FindProperty("switches");
            var current = StageReferenceAudit.ReadList<OnOffSwitch>(list);
            if (owned.Length != current.Count || owned.Any(s => current.Count(c => c == s) != 1))
            {
                changes.Add($"{root.name}/{manager.name}.switches: {current.Count} -> {owned.Length}");
                if (apply)
                {
                    list.arraySize = owned.Length;
                    for (int i = 0; i < owned.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = owned[i];
                }
            }
            foreach (string field in new[] { "activeSwitchGround", "inactiveSwitchGround" })
            {
                var property = serialized.FindProperty(field);
                string name = field == "activeSwitchGround" ? "OnSwitchGround" : "OffSwitchGround";
                var candidates = root.GetComponentsInChildren<Tilemap>(true).Where(t => t.name == name && t.GetUsedTilesCount() > 0).ToArray();
                if (managers.Length == 1 && property.objectReferenceValue == null && candidates.Length == 1)
                {
                    changes.Add($"{root.name}/{manager.name}.{field}: {candidates[0].name}");
                    if (apply) property.objectReferenceValue = candidates[0].gameObject;
                }
            }
            var belts = serialized.FindProperty("conveyorBelts");
            for (int i = belts.arraySize - 1; i >= 0; i--)
            {
                if (belts.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;
                changes.Add($"{root.name}/{manager.name}.conveyorBelts[{i}]: null 제거");
                if (apply) belts.DeleteArrayElementAtIndex(i);
            }
            if (apply) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (Conveyor belt in root.GetComponentsInChildren<Conveyor>(true))
        {
            if (managers.Any(m => StageReferenceAudit.ReadList<Conveyor>(new SerializedObject(m).FindProperty("conveyorBelts")).Contains(belt)))
                continue;
            OnOffManager[] candidates;
            if (managers.Length == 1)
                candidates = managers;
            else
            {
                candidates = belt.GetComponentsInChildren<OnOffSwitch>(true).Select(s => s.Manager)
                    .Where(m => m != null && managers.Contains(m)).Distinct().ToArray();
                if (candidates.Length == 0)
                {
                    var siblings = root.GetComponentsInChildren<Conveyor>(true).Where(b => b != belt && b.transform.parent == belt.transform.parent).ToArray();
                    candidates = managers.Where(m => StageReferenceAudit.ReadList<Conveyor>(new SerializedObject(m).FindProperty("conveyorBelts"))
                        .Any(b => siblings.Contains(b))).ToArray();
                }
            }
            if (candidates.Length != 1) continue;
            var serialized = new SerializedObject(candidates[0]);
            var list = serialized.FindProperty("conveyorBelts");
            changes.Add($"{root.name}/{candidates[0].name}.conveyorBelts: {belt.name} 추가");
            if (apply)
            {
                int index = list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = belt;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        return changes.Count - initialCount;
    }
}
