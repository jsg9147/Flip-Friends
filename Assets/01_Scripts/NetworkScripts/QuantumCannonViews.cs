using System;
using System.Collections.Generic;
using FlipFriends.Maps;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    // 예측 프레임의 탄 상태만 그린다. 탄 표시는 판정과 무관하며 롤백·재시뮬레이션마다 다시 계산되어 중복 생성되지 않는다.
    public sealed class QuantumCannonViews : IDisposable
    {
        private readonly Transform parent;
        private readonly QuantumMapConversion content;
        private readonly Sprite fallback;
        private readonly List<SpriteRenderer> pool = new();

        public QuantumCannonViews(Transform parent, Sprite fallback, QuantumMapConversion source)
        {
            this.parent = parent; this.fallback = fallback; content = source;
        }

        public int VisibleCount { get; private set; }

        public void Draw(Frame frame)
        {
            int used = 0;
            if (content.Cannons.Count > 0)
            {
                var shots = frame.Filter<FlipFriendsProjectile>();
                while (shots.Next(out _, out var shot))
                {
                    if (used == pool.Count) pool.Add(Create());
                    var r = pool[used++];
                    var sprite = content.CannonVisuals[shot.Cannon].projectileSprite;
                    r.sprite = sprite != null ? sprite : fallback;
                    r.transform.position = new Vector3(shot.Position.X.AsFloat, shot.Position.Y.AsFloat);
                    r.enabled = true;
                }
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
            VisibleCount = used;
        }

        private SpriteRenderer Create()
        {
            var go = new GameObject("대포 탄 표시");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = 8;
            return r;
        }

        public void Dispose()
        {
            foreach (var r in pool) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            pool.Clear();
        }
    }
}
