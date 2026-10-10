using UnityEditor;

// 에디터 Play를 로컬 테스트 호스트나 클라이언트로 띄운다. 빌드 쪽은 실행 인자로 반대 역할을 맡긴다.
public static class LocalNetworkTestMenu
{
    private const string Root = "Tools/Flip Friends/Local Network Test/";
    private const string HostPath = Root + "Host";
    private const string ClientPath = Root + "Client";
    private const string OffPath = Root + "Off (Steam)";

    [MenuItem(HostPath)]
    private static void SetHost() => SetMode(LocalNetworkTest.Mode.Host);

    [MenuItem(ClientPath)]
    private static void SetClient() => SetMode(LocalNetworkTest.Mode.Client);

    [MenuItem(OffPath)]
    private static void SetOff() => SetMode(LocalNetworkTest.Mode.None);

    [MenuItem(HostPath, true)]
    private static bool ValidateHost() => Check(HostPath, LocalNetworkTest.Mode.Host);

    [MenuItem(ClientPath, true)]
    private static bool ValidateClient() => Check(ClientPath, LocalNetworkTest.Mode.Client);

    [MenuItem(OffPath, true)]
    private static bool ValidateOff() => Check(OffPath, LocalNetworkTest.Mode.None);

    private static void SetMode(LocalNetworkTest.Mode mode)
    {
        EditorPrefs.SetInt(LocalNetworkTest.EditorModePrefKey, (int)mode);
    }

    private static bool Check(string path, LocalNetworkTest.Mode mode)
    {
        Menu.SetChecked(path, EditorPrefs.GetInt(LocalNetworkTest.EditorModePrefKey, 0) == (int)mode);
        return true;
    }
}
