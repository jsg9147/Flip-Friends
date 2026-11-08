using System;
using System.Collections.Generic;
using System.Linq;
using FlipFriends.Maps;
using Photon.Deterministic;
using Quantum;
using UnityEditor;
using UnityEngine;

public static class QuantumRopeChecks
{
    public const int PreparationFrames = 240;
    public const int TargetFrame = QuantumCustomMapChecks.TargetFrame + PreparationFrames;

    public static Quantum.Input InputFor(int frame, int slot)
    {
        // 실제 Photon 등록·입력 버퍼 준비가 끝난 뒤 두 참가자가 같은 시나리오를 시작한다.
        frame -= PreparationFrames;
        int outward = slot == 0 ? -1 : 1;
        int horizontal = frame >= 220 && frame < 360 || frame >= 480 && frame < 600 ? outward :
            frame >= 450 && frame < 480 || frame >= 600 && frame < 650 ? -outward : 0;
        int vertical = frame >= 90 && frame < 110 || frame >= 145 && frame < 220 ||
            frame >= 300 && frame < 390 ? 1 :
            frame >= 130 && frame < 145 || frame >= 390 && frame < 460 || frame >= 650 && frame < 690 ? -1 : 0;
        return new Quantum.Input { Horizontal = horizontal, Vertical = vertical,
            Jump = frame >= 145 && frame < 155 || frame >= 540 && frame < 570 };
    }

    [MenuItem("Tools/Flip Friends/Quantum/Check Rope Conversion And Replay")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("로프 시험 씬을 Play하세요.");
        var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>(QuantumRopeTestSetup.SourcePath);
        MapData Fresh() => JsonUtility.FromJson<MapData>(source.text);
        var baseline = QuantumMapConverter.Convert(Fresh(), palette);
        Require(baseline.Ropes.Count == 4, "로프 4개");
        var reverse = Fresh(); reverse.objects.Reverse();
        Require(baseline.CanonicalContent == QuantumMapConverter.Convert(reverse, palette).CanonicalContent, "로프 역순 동일성");
        for (int i = 0; i < 10; i++) Require(baseline.ContentHash == QuantumMapConverter.Convert(Fresh(), palette).ContentHash, "반복 변환");
        var changed = Fresh(); changed.objects[7].position.y += 0.25f;
        Require(baseline.ContentHash != QuantumMapConverter.Convert(changed, palette).ContentHash, "로프 좌표 해시");
        var box = palette.GetPrefab(QuantumMapConverter.Rope).GetComponent<BoxCollider2D>();
        var first = baseline.Ropes.Single(rope => rope.Center.X == -2);
        Require(first.Center.Y == QuantumMapConverter.Q(3) + QuantumMapConverter.Q(box.offset.y) * 3 && first.Size.Y == 3,
            "프리팹 오프셋에 배치 스케일 적용");
        changed = Fresh(); changed.objects[7].scale.y = -3;
        Require(QuantumMapConverter.Convert(changed, palette).Ropes.Any(rope => rope.Center.X == -2 && rope.Center.Y == FP.FromString("4.5")), "음수 스케일 오프셋");
        changed = Fresh(); changed.objects[7].rotation = 180;
        Require(QuantumMapConverter.Convert(changed, palette).Ropes.Any(rope => rope.Center.X == -2 && rope.Center.Y == FP.FromString("4.5")), "180도 오프셋");
        Reject(Fresh(), palette, data => data.objects[7].rotation = 90);
        Reject(Fresh(), palette, data => data.objects[7].rotation = 45);
        Reject(Fresh(), palette, data => data.objects[7].position.z = 1);
        Reject(Fresh(), palette, data => data.objects[7].scale.y = 0);
        Reject(Fresh(), palette, data => data.objects[7].scale.y = float.NaN);
        Reject(Fresh(), palette, data => data.objects[7].position.x = float.PositiveInfinity);
        Reject(Fresh(), palette, data => data.objects[7].prefabID = "interactive.box");
        foreach (var mutation in new Action<GameObject>[] {
            go => { UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider2D>()); go.AddComponent<CircleCollider2D>(); },
            go => go.GetComponent<BoxCollider2D>().isTrigger = false,
            go => go.GetComponent<BoxCollider2D>().enabled = false,
            go => go.GetComponent<BoxCollider2D>().edgeRadius = 0.1f,
            go => new GameObject("미지원 자식").transform.SetParent(go.transform),
            go => go.AddComponent<Rigidbody2D>(),
            go => go.AddComponent<BoxCollider2D>(),
            go => go.tag = "Untagged" })
            RejectPrefab(Fresh(), palette, mutation);
        CheckManifestRejection();
        QuantumCustomMapChecks.RunReplay(baseline.ContentHash, true);
    }

    private static void CheckManifestRejection()
    {
        var original = AssetDatabase.LoadAssetAtPath<QuantumCustomMapAsset>(QuantumRopeTestSetup.ManifestPath);
        foreach (string field in new[] { "conversionVersion", "paletteVersion", "contentHash" })
        {
            var copy = UnityEngine.Object.Instantiate(original);
            try
            {
                var serialized = new SerializedObject(copy);
                if (field == "contentHash") serialized.FindProperty(field).stringValue = "다른해시";
                else serialized.FindProperty(field).intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                bool isRejected = false;
                try { copy.ValidateAndApply(new RuntimeConfig()); }
                catch (InvalidOperationException) { isRejected = true; }
                Require(isRejected, "실제 맵 검증 에셋의 " + field + " 불일치 거부");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }

    private static void RejectPrefab(MapData data, MapEditorPalette original, Action<GameObject> mutation)
    {
        var palette = ScriptableObject.CreateInstance<MapEditorPalette>();
        var prefab = UnityEngine.Object.Instantiate(original.GetPrefab(QuantumMapConverter.Rope));
        try
        {
            mutation(prefab);
            palette.entries = original.entries.Select(e => new PaletteEntry { id = e.id,
                prefab = e.id == QuantumMapConverter.Rope ? prefab : e.prefab, validationRule = e.validationRule }).ToList();
            Reject(data, palette, _ => { });
        }
        finally { UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(palette); }
    }

    private static void Reject(MapData data, MapEditorPalette palette, Action<MapData> mutation)
    {
        mutation(data);
        bool isRejected = false;
        try { QuantumMapConverter.Convert(data, palette); }
        catch (InvalidOperationException error)
        {
            isRejected = !string.IsNullOrEmpty(error.Message);
            Require(error.Message.Contains("위치="), "미지원 로프 위치 진단: " + error.Message);
        }
        Require(isRejected, "미지원 로프 전체 변환 거부");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("로프 자동 검사 실패: " + message);
    }

    public sealed class RopeEvidence : IQuantumMovementEvidence
    {
        private readonly HashSet<string>[] passed = { new(), new() };
        private readonly FlipFriendsPlayer[] previous = new FlipFriendsPlayer[2];
        private readonly FPVector2[] positions = new FPVector2[2];
        private readonly int[] lastFrame = new int[2];
        private readonly int[] jumpCount = new int[2];
        private readonly int[] observedFrames = new int[2], maxFrameGap = new int[2];
        private readonly List<string> trace = new();
        private static readonly string[] Required = { "생성", "자동진입없음", "진입", "상승", "하강", "정지", "끝점이탈", "수평이탈",
            "점프분리", "재등반차단", "재진입", "벽충돌", "천장충돌", "바닥충돌", "벽미끄럼자동활성없음" };
        public string Summary => string.Join("\n", Enumerable.Range(0, 2).Select(i =>
            $"플레이어 {i}: {string.Join(", ", passed[i].OrderBy(x => x, StringComparer.Ordinal))}, 로프 점프 {jumpCount[i]}회, 확정 관찰 {observedFrames[i]}개/최대 간격 {maxFrameGap[i]}")) + "\n" + string.Join("\n", trace);

        public void Observe(Frame frame)
        {
            var query = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (query.Next(out var entity, out var transform, out var player))
            {
                int slot = (int)player.Player;
                Require(slot >= 0 && slot < 2, "슬롯 범위");
                if (frame.Number <= lastFrame[slot]) continue;
                var p = transform.Position;
                int scriptFrame = frame.Number - PreparationFrames;
                var old = previous[slot];
                if (frame.Number % 30 == 0)
                {
                    var input = InputFor(frame.Number, slot);
                    var actual = frame.FlipFriendsInput(player.Player);
                    trace.Add($"확정 {frame.Number} 슬롯 {slot}: 위치={p} 속도={player.Velocity} 요청입력={input.Horizontal}/{input.Vertical} 실제={actual.Horizontal}/{actual.Vertical}/점프{actual.Jump.IsDown} 등반={player.IsClimbed} 접촉={player.RopeContactCount} 제한={player.ClimbBlockTicks}");
                }
                bool hasPrevious = lastFrame[slot] > 0;
                if (hasPrevious) maxFrameGap[slot] = Math.Max(maxFrameGap[slot], frame.Number - lastFrame[slot]);
                observedFrames[slot]++;
                if (hasPrevious && old.ClimbBlockTicks > 0)
                    Require(player.ClimbBlockTicks == Math.Max(0, old.ClimbBlockTicks - (frame.Number - lastFrame[slot])), "프레임 기준 제한 타이머 감소");
                void Mark(bool condition, string key) { if (condition) passed[slot].Add(key); }
                Require(p.X >= FP.FromString("-5.101") && p.X <= FP.FromString("5.101") &&
                    p.Y >= FP.FromString("0.499") && p.Y <= FP.FromString("4.501"), "지형 비관통");
                if (lastFrame[slot] == 0)
                {
                    var expected = frame.RuntimeConfig.CustomMapSpawns[slot];
                    Require(FPMath.Abs(p.X - expected.X) < FP.FromString("0.067") && FPMath.Abs(p.Y - expected.Y) < FP.FromString("0.02"), "지정 위치 생성");
                    passed[slot].Add("생성");
                }
                if (scriptFrame < 90) Require(!player.IsClimbed, "상하 입력 없는 진입 금지");
                Mark(scriptFrame < 90 && player.RopeContactCount > 0 && !player.IsClimbed, "자동진입없음");
                Mark(hasPrevious && !old.IsClimbed && player.IsClimbed, "진입");
                Mark(player.IsClimbed && player.Velocity.Y == 4, "상승");
                Mark(player.IsClimbed && player.Velocity.Y == -4, "하강");
                Mark(hasPrevious && lastFrame[slot] - PreparationFrames >= 110 && scriptFrame < 130 && old.IsClimbed && player.IsClimbed &&
                    old.Velocity == FPVector2.Zero && player.Velocity == FPVector2.Zero && p == positions[slot], "정지");
                Mark(hasPrevious && old.IsClimbed && !player.IsClimbed && player.RopeContactCount == 0 && p.Y > FP.FromString("3.5"), "끝점이탈");
                Mark(hasPrevious && old.IsClimbed && !player.IsClimbed && player.RopeContactCount == 0 && scriptFrame > 450 && scriptFrame < 480, "수평이탈");
                if (hasPrevious && old.IsClimbed && old.ClimbBlockTicks == 0 && player.ClimbBlockTicks > 0)
                {
                    Require(!player.IsClimbed && player.Velocity.Y > 0, "점프 분리 상승");
                    if (player.ClimbBlockTicks == 17) Require(player.Velocity.Y > 12, "점프 첫 틱 속도");
                    passed[slot].Add("점프분리"); jumpCount[slot]++;
                }
                if (player.ClimbBlockTicks > 0)
                {
                    Require(!player.IsClimbed, "18틱 제한 중 재진입 금지");
                    Mark(player.RopeContactCount > 0, "재등반차단");
                }
                Mark(hasPrevious && old.ClimbBlockTicks > 0 && player.ClimbBlockTicks == 0 && player.IsClimbed, "재진입");
                Mark(player.IsClimbed && (player.IsTouchingLeft || player.IsTouchingRight), "벽충돌");
                Mark(player.IsClimbed && player.IsTouchingCeiling, "천장충돌");
                Mark(player.IsClimbed && player.IsGrounded, "바닥충돌");
                Mark(!player.IsClimbed && (player.IsTouchingLeft || player.IsTouchingRight) && player.Velocity.Y < -3, "벽미끄럼자동활성없음");
                previous[slot] = player; positions[slot] = p; lastFrame[slot] = frame.Number;
            }
        }

        public void AssertComplete()
        {
            for (int i = 0; i < 2; i++)
                Require(Required.All(passed[i].Contains) && jumpCount[i] == 1,
                    "두 플레이어 전이 누락: " + string.Join(", ", Required.Except(passed[i])) + "\n" + Summary);
        }
    }
}
