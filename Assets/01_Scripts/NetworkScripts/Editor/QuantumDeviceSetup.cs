// 장치 사용례 콘텐츠의 경로. 생성기는 Mirror 원본 SavePoint·RespawnHandler·ObjSummonSwitch에서 값을 읽었으므로
// Mirror 제거와 함께 지웠다. 결과물(JSON·장치·베이크·매니페스트)은 커밋된 에셋이 기준이며 원본 값은
// Docs/Notes/2026-11-15-mirror-device-values.md에 남겼다.
public static class QuantumDeviceSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsDeviceMapManifest.asset";
    public const string SourcePath = "Assets/QuantumUser/Resources/FlipFriendsDeviceMap.json";
    public const string DevicesPath = "Assets/QuantumUser/Resources/FlipFriendsDeviceContent.asset";
    public const string ScenePath = "Assets/00_Scenes/QuantumDeviceTest.unity";
}
