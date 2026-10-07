// 검증기를 ScriptableObject와 Mirror 없이 테스트할 수 있도록 팔레트 조회만 노출한다.
public interface IMapPaletteLookup
{
    MapValidationSettings ValidationSettings { get; }

    bool TryGetEntry(string prefabID, out PaletteEntry entry);
}
