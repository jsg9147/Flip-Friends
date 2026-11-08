using System;
using System.Collections.Generic;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumBoxViews : IDisposable
    {
        private readonly Dictionary<EntityRef, GameObject> views = new();
        private readonly Transform parent;
        private readonly Sprite sprite;
        public QuantumBoxViews(Transform parent, Sprite sprite) { this.parent = parent; this.sprite = sprite; }
        public void Draw(Frame frame)
        {
            var active = new HashSet<EntityRef>();
            var boxes = frame.Filter<Transform2D, FlipFriendsBox>();
            while (boxes.Next(out var entity, out var transform, out var box))
            {
                active.Add(entity);
                if (!views.TryGetValue(entity, out var view))
                {
                    view = new GameObject("Quantum 상자 표시");
                    view.transform.SetParent(parent, false);
                    var renderer = view.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = new Color(0.85f, 0.5f, 0.2f);
                    renderer.sortingOrder = 11;
                    views.Add(entity, view);
                }
                view.SetActive(!box.RespawnPending);
                view.transform.position = new Vector3(transform.Position.X.AsFloat, transform.Position.Y.AsFloat, 0);
                view.transform.localScale = new Vector3(box.HalfSize.X.AsFloat * 2, box.HalfSize.Y.AsFloat * 2, 1);
            }
            var removed = new List<EntityRef>();
            foreach (var pair in views) if (!active.Contains(pair.Key)) { UnityEngine.Object.Destroy(pair.Value); removed.Add(pair.Key); }
            foreach (var entity in removed) views.Remove(entity);
        }
        public void Dispose()
        {
            foreach (var view in views.Values) if (view != null) UnityEngine.Object.Destroy(view);
            views.Clear();
        }
    }
}
