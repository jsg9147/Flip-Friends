// Tilemap 사용례 콘텐츠의 경로. 생성기·재계산기는 원본 Stage 1 Ground와 Stage 9 RespawnHandler를 읽었으므로
// Mirror 제거와 함께 지웠다. 결과물(JSON·지형·장치·베이크·매니페스트)은 커밋된 에셋이 기준이다.
public static class QuantumTilemapSetup
{
    public const string ManifestPath = "Assets/QuantumUser/Resources/FlipFriendsTilemapMapManifest.asset";
    public const string ScenePath = "Assets/00_Scenes/QuantumTilemapTest.unity";
}
