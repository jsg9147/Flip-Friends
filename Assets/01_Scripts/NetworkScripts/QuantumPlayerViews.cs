using System;
using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumPlayerViews : IDisposable
    {
        private readonly Dictionary<EntityRef, GameObject> views = new();
        private readonly Transform parent;
        private readonly Sprite sprite;
        private readonly bool ownsSprite;
        private readonly AnimationClip[] clips;
        private static readonly Color[] Colors = { Color.cyan, Color.yellow, new(1f, 0.5f, 0.7f), new(0.65f, 1f, 0.4f) };
        public int Count => views.Count;

        public QuantumPlayerViews(Transform parent, Sprite sprite = null, AnimationClip[] clips = null)
        {
            this.parent = parent;
            this.clips = clips;
            ownsSprite = sprite == null;
            this.sprite = sprite != null ? sprite : Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one / 2, 1);
        }

        public void Draw(Frame frame)
        {
            var active = new HashSet<EntityRef>();
            var players = frame.Filter<Transform2D, FlipFriendsPlayer>();
            while (players.Next(out var entity, out var transform, out var player))
            {
                active.Add(entity);
                if (!views.TryGetValue(entity, out var view))
                {
                    view = new GameObject($"Quantum 플레이어 {player.Player}");
                    view.transform.SetParent(parent, false);
                    var renderer = view.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = Colors[(int)player.Player % Colors.Length];
                    renderer.sortingOrder = 10;
                    view.transform.localScale = new Vector3(0.8f / sprite.bounds.size.x, 1 / sprite.bounds.size.y, 1);
                    views.Add(entity, view);
                }
                view.SetActive(!player.IsFinished && !player.RespawnPending);
                view.transform.position = new Vector3(transform.Position.X.AsFloat, transform.Position.Y.AsFloat, 0);
                var body = view.GetComponent<SpriteRenderer>();
                if (clips?.Length == 7)
                {
                    int pose = player.DamageCount > 0 && frame.Number - player.DamageFrame < 15 ? 5 :
                        player.ShrinkTicks > 0 ? 6 : player.Carrier.IsValid ? 0 : player.IsClimbed ? 4 :
                        !player.IsGrounded ? (player.Velocity.Y > 0 ? 2 : 3) :
                        player.Velocity.X != 0 ? 1 : 0;
                    var clip = clips[pose];
                    if (clip != null && clip.length > 0)
                    {
                        float time = pose == 6 ? (30 - player.ShrinkTicks) / 60f : frame.Number / 60f % clip.length;
                        clip.SampleAnimation(view, time);
                    }
                }
                var size = body.sprite.bounds.size;
                view.transform.localScale = new Vector3(0.8f / size.x, (player.ShrinkTicks > 0 ? 0.5f : 1f) / size.y, 1);
                if (player.ShrinkTicks > 0) view.transform.position += Vector3.down * 0.25f;
                body.flipX = player.Facing < 0;
                // 피격 연출은 이벤트가 아니라 프레임의 무적 틱에서 다시 계산한다. 예측 재시뮬레이션으로 중복 재생되지 않는다.
                var color = Colors[(int)player.Player % Colors.Length];
                if (player.InvincibleTicks > 0 && player.InvincibleTicks / 6 % 2 == 1) color.a = 0.35f;
                body.color = color;
            }
            var removed = new List<EntityRef>();
            foreach (var pair in views)
                if (!active.Contains(pair.Key)) { UnityEngine.Object.Destroy(pair.Value); removed.Add(pair.Key); }
            foreach (var entity in removed) views.Remove(entity);
        }

        public void Dispose()
        {
            foreach (var view in views.Values) if (view != null) UnityEngine.Object.Destroy(view);
            views.Clear();
            if (ownsSprite) UnityEngine.Object.Destroy(sprite);
        }
    }
}
