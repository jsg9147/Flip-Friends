using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class StageReferenceAuditChecks
{
    [MenuItem("Tools/Flip Friends/Stages/Check Audit And Repair")]
    public static void Run()
    {
        string path = StageReferenceAudit.StageFolder + "/Stage 11.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var manager = root.GetComponentInChildren<OnOffManager>(true);
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("switches").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var report = new StageReferenceAudit.Report();
            StageReferenceAudit.AuditRoot(root, report);
            Require(report.issues.Count(i => i.code == "UnlistedSwitch") == 11, "누락 스위치 11개 검출");
            Require(!report.issues.Any(i => i.field == "conveyorBelts"), "벨트 없는 스테이지의 빈 목록 허용");

            var changes = new List<string>();
            Require(StageOnOffRepair.RepairRoot(root, false, changes) > 0, "미리보기 후보 존재");
            serialized.Update();
            Require(serialized.FindProperty("switches").arraySize == 0, "미리보기 무변경");
            changes.Clear();
            Require(StageOnOffRepair.RepairRoot(root, true, changes) > 0, "역참조 복구");
            report = new StageReferenceAudit.Report();
            StageReferenceAudit.AuditRoot(root, report);
            Require(!report.issues.Any(i => i.code == "UnlistedSwitch"), "복구 후 누락 없음");
            changes.Clear();
            Require(StageOnOffRepair.RepairRoot(root, true, changes) == 0, "재실행 멱등성");

            serialized.Update();
            var list = serialized.FindProperty("switches");
            list.GetArrayElementAtIndex(1).objectReferenceValue = list.GetArrayElementAtIndex(0).objectReferenceValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            report = new StageReferenceAudit.Report();
            StageReferenceAudit.AuditRoot(root, report);
            Require(report.issues.Any(i => i.code == "DuplicateReference"), "중복 참조 검출");
            Debug.Log("StageReferenceAuditChecks: PASS (누락/선택 null/미리보기/복구/멱등성/중복)");
        }
        finally
        {
            // 저장하지 않은 격리 복사본으로 실제 스테이지를 훼손하지 않는다.
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("스테이지 검사 실패: " + name);
    }
}
