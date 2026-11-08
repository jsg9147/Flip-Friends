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
        public int Count => views.Count;

        public QuantumPlayerViews(Transform parent, Sprite sprite = null)
        {
            this.parent = parent;
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
                    renderer.color = (int)player.Player == 0 ? Color.cyan : Color.yellow;
                    renderer.sortingOrder = 10;
                    view.transform.localScale = new Vector3(0.8f / sprite.bounds.size.x, 1 / sprite.bounds.size.y, 1);
                    views.Add(entity, view);
                }
                view.SetActive(!player.IsFinished && !player.RespawnPending);
                view.transform.position = new Vector3(transform.Position.X.AsFloat, transform.Position.Y.AsFloat, 0);
                view.transform.localScale = new Vector3(0.8f / sprite.bounds.size.x, (player.ShrinkTicks > 0 ? 0.5f : 1f) / sprite.bounds.size.y, 1);
                if (player.ShrinkTicks > 0) view.transform.position += Vector3.down * 0.25f;
                view.GetComponent<SpriteRenderer>().flipX = player.Facing < 0;
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
