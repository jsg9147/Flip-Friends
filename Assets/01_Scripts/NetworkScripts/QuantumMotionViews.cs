using System;
using FlipFriends.Maps;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumMotionViews : IDisposable
    {
        private readonly SpriteRenderer[] views;
        private readonly SpriteRenderer[] switches;
        private readonly bool[] doors;
        private readonly QuantumMapConversion content;
        private static readonly Color ClockwiseColor = new(.35f, .43f, .88f, 1);
        private static readonly Color CounterclockwiseColor = new(.86f, .29f, .3f, 1);

        public QuantumMotionViews(Transform parent, Sprite fallback, QuantumMapConversion source)
        {
            content = source; views = new SpriteRenderer[source.MotionVisuals.Count];
            doors = new bool[views.Length];
            switches = new SpriteRenderer[source.StageSwitches.Count];
            for (int i = 0; i < switches.Length; i++)
            {
                var button = source.StageSwitches[i];
                if (button.IsDoor && button.Target >= 0) doors[button.Target] = true;
                var go = new GameObject("스위치 표시 " + button.Id);
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(button.Center.X.AsFloat, button.Center.Y.AsFloat);
                go.transform.localScale = new Vector3(2 * button.HalfSize.X.AsFloat, 2 * button.HalfSize.Y.AsFloat, 1);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = fallback;
                renderer.sortingOrder = 7;
                switches[i] = renderer;
            }
            for (int i = 0; i < views.Length; i++)
            {
                var s = source.MotionVisuals[i]; var go = new GameObject("이동 장치 표시 " + s.id);
                go.transform.SetParent(parent, false); var r = go.AddComponent<SpriteRenderer>();
                r.sprite = s.sprite != null ? s.sprite : fallback; r.sortingOrder = 6;
                var size = r.sprite.bounds.size; go.transform.localScale = new Vector3(s.renderSize.x / size.x, s.renderSize.y / size.y, 1);
                views[i] = r;
            }
        }

        public void Draw(Frame frame)
        {
            var q = frame.Filter<FlipFriendsMotion>();
            while (q.Next(out _, out var s))
            {
                var r = views[s.ContentIndex]; var source = content.MotionVisuals[s.ContentIndex];
                float angle = s.Angle.AsFloat; var offset = source.renderCenter - source.origin;
                var rotated = new Vector2(offset.x * Mathf.Cos(angle) - offset.y * Mathf.Sin(angle), offset.x * Mathf.Sin(angle) + offset.y * Mathf.Cos(angle));
                r.transform.position = new Vector3(s.Position.X.AsFloat + rotated.x, s.Position.Y.AsFloat + rotated.y);
                r.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
                float alpha = source.disappearAfterComplete && s.Phase == 3 ? 0 : 1;
                if (source.kind == 4) alpha = s.Collidable ? 1 : doors[s.ContentIndex] ? 0 : .15f;
                if (source.disableColliderWhenFalling && s.Phase != 0) alpha *= 1 - Mathf.Clamp01(s.ElapsedTicks / 30f);
                r.color = new Color(1, 1, 1, alpha);
            }
            // 원본 Conveyor 색: 시계(왼쪽) 파랑, 반시계(오른쪽) 빨강. 판정이 꺼진 발판의 투명도는 유지한다.
            var belts = frame.Filter<FlipFriendsConveyor>();
            while (belts.Next(out _, out var belt))
            {
                var r = views[content.Conveyors[belt.ContentIndex].Motion];
                var color = belt.Clockwise ? ClockwiseColor : CounterclockwiseColor;
                color.a = r.color.a; r.color = color;
            }
            var buttons = frame.Filter<FlipFriendsStageSwitch>();
            while (buttons.Next(out _, out var button))
                switches[button.ContentIndex].color = button.Active ? new Color(.25f, .85f, .45f) : new Color(.95f, .55f, .15f);
        }

        public void Dispose()
        {
            foreach (var r in views) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            foreach (var r in switches) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
        }
    }
}
