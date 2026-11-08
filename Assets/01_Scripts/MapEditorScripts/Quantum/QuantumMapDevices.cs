using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Deterministic;
using Quantum;
using UnityEngine;

namespace FlipFriends.Maps
{
    [Serializable]
    public sealed class QuantumDeviceSource
    {
        public string id;
        public string authoringSignature;
        public int kind;
        public int checkpointId;
        public Vector2 center, size, destination, targetBoxHome;
        public Vector2[] contactCenters, contactSizes;
        public bool onlyBoxReset, detectPlayers, detectBoxes;
        public float cooldownSeconds;
        public Vector2 renderCenter, renderSize;
        public Sprite defaultSprite, pressedSprite;
    }

    public sealed class QuantumMapDevices : ScriptableObject
    {
        [SerializeField] private QuantumDeviceSource[] devices;
        public QuantumDeviceSource[] Sources => devices;
        public void Configure(QuantumDeviceSource[] sources) => devices = sources;

        public void Apply(QuantumMapConversion conversion)
        {
            if (devices == null || devices.Length == 0 || devices.Length > 32 || conversion.MovableBoxes.Count > 64)
                throw new InvalidOperationException("장치 콘텐츠는 1~32개, 참조 상자는 최대64개여야 합니다.");
            if (devices.Sum(d => d?.contactCenters?.Length ?? 1) > 512)
                throw new InvalidOperationException("장치 접촉 조각 총합(512)을 초과했습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var lines = new List<string>();
            foreach (var source in devices.OrderBy(s => s?.id, StringComparer.Ordinal))
            {
                try
                {
                    if (source == null || string.IsNullOrWhiteSpace(source.id) || !ids.Add(source.id))
                        throw new InvalidOperationException("안정적인 장치 ID가 없거나 중복입니다.");
                    if (source.kind < 0 || source.kind > 2 || source.checkpointId < 0)
                        throw new InvalidOperationException("미지원 장치 종류 또는 음수 체크포인트 ID입니다.");
                    var config = new FlipFriendsDeviceConfig { Id = source.id, Kind = source.kind, CheckpointId = source.checkpointId,
                        Center = Q(source.center), Size = Q(source.size), Destination = Q(source.destination),
                        OnlyBoxReset = source.onlyBoxReset, DetectPlayers = source.detectPlayers, DetectBoxes = source.detectBoxes,
                        TargetBoxIndex = -1 };
                    if (config.Size.X <= 0 || config.Size.Y <= 0 || !source.detectPlayers && !source.detectBoxes)
                        throw new InvalidOperationException("판정 크기 또는 접촉 대상이 잘못되었습니다.");
                    if ((source.contactCenters?.Length ?? 0) != (source.contactSizes?.Length ?? 0) ||
                        !string.IsNullOrEmpty(source.authoringSignature) && (source.contactCenters?.Length ?? 0) == 0)
                        throw new InvalidOperationException("Reset 조각 배열이 누락되었거나 비어 있습니다.");
                    if (source.contactCenters != null && source.contactCenters.Length > 0)
                    {
                        if (source.kind != 1 || source.contactSizes == null || source.contactCenters.Length != source.contactSizes.Length || source.contactCenters.Length > 256)
                            throw new InvalidOperationException("Reset 조각 구성/상한이 잘못되었습니다.");
                        var fragments = source.contactCenters.Select((c, i) => (center: Q(c), size: Q(source.contactSizes[i])))
                            .OrderBy(p => Key(p.center) + "/" + Key(p.size), StringComparer.Ordinal).ToArray();
                        if (fragments.Any(p => p.size.X <= 0 || p.size.Y <= 0) || fragments.Select(p => Key(p.center) + "/" + Key(p.size)).Distinct().Count() != fragments.Length)
                            throw new InvalidOperationException("Reset 조각이 중복이거나 크기가 잘못되었습니다.");
                        config.ContactCenters = fragments.Select(p => p.center).ToArray(); config.ContactSizes = fragments.Select(p => p.size).ToArray();
                        lines.Add("Reset조각=" + source.id + ";" + string.Join(";", fragments.Select(p => Key(p.center) + "/" + Key(p.size))));
                        if (string.IsNullOrWhiteSpace(source.authoringSignature)) throw new InvalidOperationException("Reset 작성 서명이 없습니다.");
                        lines.Add("Reset작성=" + source.authoringSignature);
                    }
                    if (source.kind != 1 && (!source.detectPlayers || source.detectBoxes))
                        throw new InvalidOperationException("체크포인트/소환은 Player 진입만 지원합니다.");
                    if (source.renderSize.x <= 0 || source.renderSize.y <= 0)
                        throw new InvalidOperationException("경계에 반영할 장치 렌더러 범위가 없습니다.");
                    var renderCenter = Q(source.renderCenter); var renderSize = Q(source.renderSize);
                    if (config.ContactCenters != null)
                    for (int i = 0; i < config.ContactCenters.Length; i++)
                        if (FPMath.Abs(config.ContactCenters[i].X - renderCenter.X) + config.ContactSizes[i].X / 2 > renderSize.X / 2 ||
                            FPMath.Abs(config.ContactCenters[i].Y - renderCenter.Y) + config.ContactSizes[i].Y / 2 > renderSize.Y / 2)
                            throw new InvalidOperationException("Reset 접촉 조각이 렌더러 경계를 벗어났습니다.");
                    conversion.MinX = FPMath.Min(conversion.MinX, renderCenter.X - renderSize.X / 2 - 10);
                    conversion.MaxX = FPMath.Max(conversion.MaxX, renderCenter.X + renderSize.X / 2 + 10);
                    conversion.MinY = FPMath.Min(conversion.MinY, renderCenter.Y - renderSize.Y / 2 - 10);
                    if (source.kind == 2)
                    {
                        if (!float.IsFinite(source.cooldownSeconds) || source.cooldownSeconds < 0 || source.cooldownSeconds > 3600)
                            throw new InvalidOperationException("소환 쿨다운은 유한한 0~3600초여야 합니다.");
                        config.CooldownTicks = checked((int)Math.Ceiling((double)source.cooldownSeconds * 60));
                        var matches = conversion.MovableBoxes.Select((box, index) => (box, index)).Where(p => p.box.Center == Q(source.targetBoxHome)).ToArray();
                        if (matches.Length != 1) throw new InvalidOperationException("소환 대상 상자 초기 좌표 참조가 누락되거나 중복입니다.");
                        config.TargetBoxIndex = matches[0].index;
                    }
                    conversion.Devices.Add(config);
                    conversion.DeviceVisuals.Add(source);
                    lines.Add(FormattableString.Invariant($"장치={source.id.Length}:{source.id};종류={config.Kind};ID={config.CheckpointId};판정={Key(config.Center)}/{Key(config.Size)};목적지={Key(config.Destination)};조건={config.OnlyBoxReset}/{config.DetectPlayers}/{config.DetectBoxes};상자={config.TargetBoxIndex};쿨다운={config.CooldownTicks};표시={Key(renderCenter)}/{Key(renderSize)}"));
                }
                catch (Exception error) { throw new InvalidOperationException($"장치 {source?.id} 위치={source?.center}: {error.Message}", error); }
            }
            foreach (var device in conversion.Devices)
            {
                var half = device.Kind == 2 ? conversion.MovableBoxes[device.TargetBoxIndex].Size / 2 : new FPVector2(QuantumMapConverter.Q(0.4f), QuantumMapConverter.Q(0.5f));
                if (device.Destination.X < conversion.MinX || device.Destination.X > conversion.MaxX || device.Destination.Y < conversion.MinY ||
                    conversion.Boxes.Any(box => FPMath.Abs(device.Destination.X - box.Center.X) < half.X + box.Size.X / 2 && FPMath.Abs(device.Destination.Y - box.Center.Y) < half.Y + box.Size.Y / 2))
                    throw new InvalidOperationException($"장치 {device.Id} 위치={device.Center}: 목적지가 경계 밖이거나 지형과 겹칩니다.");
            }
            conversion.HasDevices = true;
            conversion.CanonicalContent = conversion.CanonicalContent.Replace("변환=4;팔레트=4;", "변환=5;팔레트=5;") + "\n" + string.Join("\n", lines) +
                FormattableString.Invariant($"\n장치포함경계={conversion.MinX.RawValue},{conversion.MaxX.RawValue},{conversion.MinY.RawValue}");
        }

        private static FPVector2 Q(Vector2 value) => new(QuantumMapConverter.Q(value.x), QuantumMapConverter.Q(value.y));
        private static string Key(FPVector2 value) => FormattableString.Invariant($"{value.X.RawValue},{value.Y.RawValue}");
    }
}
