using System;
using System.Linq;
using Quantum;
using UnityEngine;

namespace FlipFriends.Maps
{
    public sealed class QuantumCustomMapAsset : ScriptableObject
    {
        [SerializeField] private TextAsset source;
        [SerializeField] private MapEditorPalette palette;
        [SerializeField] private Quantum.Map bakedMap;
        [SerializeField] private string contentHash;
        [SerializeField] private int conversionVersion;
        [SerializeField] private int paletteVersion;
        [SerializeField] private QuantumMapDevices devices;
        [SerializeField] private QuantumMapTerrain terrain;
        public void ConfigureTerrain(QuantumMapTerrain value) => terrain = value;

        public void ConfigureDevices(QuantumMapDevices value) => devices = value;

        public string ContentHash => contentHash;
        public string Agreement => $"{conversionVersion}:{paletteVersion}:{contentHash}";

        public void Configure(TextAsset mapSource, MapEditorPalette mapPalette, Quantum.Map map, QuantumMapConversion conversion)
        {
            source = mapSource;
            palette = mapPalette;
            bakedMap = map;
            contentHash = conversion.ContentHash;
            conversionVersion = conversion.Version;
            paletteVersion = conversion.Version;
        }

        public QuantumMapConversion ValidateAndApply(RuntimeConfig config)
        {
            if (source == null || bakedMap == null || conversionVersion < 2 || conversionVersion > 6 || paletteVersion != conversionVersion ||
                (conversionVersion >= 5) != (devices != null) || (conversionVersion == 6) != (terrain != null))
                throw new InvalidOperationException("Quantum 맵 연결 또는 변환/팔레트 버전이 일치하지 않습니다.");
            var conversion = QuantumMapConverter.Convert(JsonUtility.FromJson<MapData>(source.text), palette, conversionVersion >= 4);
            if (devices != null) devices.Apply(conversion);
            if (terrain != null) terrain.Apply(conversion);
            if (conversionVersion != conversion.Version) throw new InvalidOperationException("콘텐츠에 맞는 변환/팔레트 버전이 아닙니다.");
            if (conversion.ContentHash != contentHash || bakedMap.StaticColliders2D.Length != conversion.Boxes.Count ||
                bakedMap.MapEntities.Length != 0 || bakedMap.StaticColliders3D.Length != 0)
                throw new InvalidOperationException("맵 콘텐츠 해시 또는 베이크 구성이 일치하지 않습니다.");
            var actual = bakedMap.StaticColliders2D.Select(box =>
            {
                if (box.ShapeType != Shape2DType.Box || box.Rotation != 0 || box.Layer != 0 || box.StaticData.IsTrigger)
                    throw new InvalidOperationException("베이크된 정적 박스의 판정 설정이 다릅니다.");
                return BoxKey(box.Position, box.BoxExtents * 2);
            }).OrderBy(key => key, StringComparer.Ordinal);
            var expected = conversion.Boxes.Select(box => BoxKey(box.Center, box.Size)).OrderBy(key => key, StringComparer.Ordinal);
            if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("베이크된 지형 좌표/크기가 원본 변환과 다릅니다.");
            config.Map = bakedMap;
            config.CustomMapAgreement = Agreement;
            config.CustomMapSpawns = conversion.Spawns.ToArray();
            config.CustomMapRopeCenters = conversion.Ropes.Select(rope => rope.Center).ToArray();
            config.CustomMapRopeSizes = conversion.Ropes.Select(rope => rope.Size).ToArray();
            config.BoxCenters = conversion.MovableBoxes.Select(box => box.Center).ToArray();
            config.BoxSizes = conversion.MovableBoxes.Select(box => box.Size).ToArray();
            config.Devices = conversion.Devices.ToArray();
            config.TerrainContours = conversion.TerrainContours;
            return conversion;
        }

        public void ApplyRespawn(RuntimeConfig config, QuantumMapConversion conversion)
        {
            if (devices == null) QuantumMapConverter.ApplyRespawn(JsonUtility.FromJson<MapData>(source.text), palette, conversion);
            config.EnableRespawn = true;
            config.FallMinX = conversion.MinX; config.FallMaxX = conversion.MaxX; config.FallMinY = conversion.MinY;
        }

        private static string BoxKey(Photon.Deterministic.FPVector2 position, Photon.Deterministic.FPVector2 size) =>
            $"{position.X.RawValue},{position.Y.RawValue},{size.X.RawValue},{size.Y.RawValue}";
    }
}
