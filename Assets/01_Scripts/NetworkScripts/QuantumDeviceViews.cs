using System;
using System.Collections.Generic;
using FlipFriends.Maps;
using Quantum;
using TMPro;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumDeviceViews : IDisposable
    {
        private readonly List<GameObject> roots = new();
        private readonly List<SpriteRenderer> sprites = new();
        private readonly List<TextMeshPro> labels = new();
        private readonly QuantumMapConversion content;
        public QuantumDeviceViews(Transform parent, Sprite fallback, TMP_FontAsset font, QuantumMapConversion content)
        {
            this.content = content;
            foreach (var source in content.DeviceVisuals)
            {
                var root = new GameObject("Quantum 장치 표시 " + source.id);
                root.transform.SetParent(parent, false);
                root.transform.position = source.renderCenter;
                var sprite = root.AddComponent<SpriteRenderer>();
                sprite.sprite = source.defaultSprite != null ? source.defaultSprite : fallback;
                sprite.sortingOrder = 5;
                if (source.kind == 1) sprite.color = new Color(1, 0.25f, 0.3f);
                var size = sprite.sprite.bounds.size;
                root.transform.localScale = new Vector3(source.renderSize.x / size.x, source.renderSize.y / size.y, 1);
                if (source.defaultSprite == null) sprite.color = new Color(0.8f, 0.2f, 0.4f, 0.5f);
                if (source.contactCenters != null && source.contactCenters.Length > 0)
                {
                    sprite.enabled = false;
                    for (int i = 0; i < source.contactCenters.Length; i++)
                    {
                        var piece = new GameObject("Reset 접촉 조각"); piece.transform.SetParent(root.transform, false);
                        piece.transform.position = source.contactCenters[i];
                        piece.transform.localScale = new Vector3(source.contactSizes[i].x / root.transform.localScale.x, source.contactSizes[i].y / root.transform.localScale.y, 1);
                        var part = piece.AddComponent<SpriteRenderer>(); part.sprite = fallback; part.color = new Color(1, 0.2f, 0.3f, 0.5f); part.sortingOrder = 5;
                    }
                }
                var labelRoot = new GameObject("장치 상태"); labelRoot.transform.SetParent(parent, false);
                labelRoot.transform.position = source.renderCenter + Vector2.up * (source.renderSize.y / 2 + 0.4f);
                var label = labelRoot.AddComponent<TextMeshPro>(); label.font = font; label.fontSize = 2;
                label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(5, 1);
                label.GetComponent<MeshRenderer>().sortingOrder = 15;
                roots.Add(root); roots.Add(labelRoot); sprites.Add(sprite); labels.Add(label);
            }
        }
        public void Draw(Frame frame)
        {
            var query = frame.Filter<FlipFriendsDevice>();
            while (query.Next(out _, out var state))
            {
                int i = state.ContentIndex; var config = content.Devices[i]; var source = content.DeviceVisuals[i];
                int ticks = Math.Max(0, state.CooldownUntil - frame.Number);
                if (source.defaultSprite != null) sprites[i].sprite = ticks > 0 && source.pressedSprite != null ? source.pressedSprite : source.defaultSprite;
                labels[i].text = config.Kind == 0 ? "체크포인트 " + config.CheckpointId : config.Kind == 1 ? (config.OnlyBoxReset ? "상자를 들고 구간 복귀" : "구간 복귀") : ticks > 0 ? $"소환 {(ticks + 59) / 60}초" : "상자 소환";
            }
        }
        public void Dispose() { foreach (var root in roots) if (root != null) UnityEngine.Object.Destroy(root); roots.Clear(); }
    }
}
