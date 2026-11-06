#if UNITY_EDITOR
// 실제 씬 전환 없이 서버의 복귀 요청 횟수만 검사하는 편집기 하네스다.
public class StageLifecycleTestRoomManager : SlimeRoomManager
{
    public int ReturnCount { get; private set; }
    public override void Awake() { }
    public override void OnDestroy() { }
    public override void ReturnRoomScene() => ReturnCount++;
}

#endif
