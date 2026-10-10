using System;
using System.Collections;
using kcp2k;
using Mirror;
using UnityEngine;

// Steam 계정 하나로는 FizzySteamworks가 자기 자신에게 접속할 수 없어 2인 동기화를 확인할 길이 없다.
// 개발 중에만 KCP + LatencySimulation으로 갈아 끼워 한 PC에서 지연이 있는 호스트·클라이언트를 띄운다.
// 빌드는 실행 인자(-localhost / -localclient, -latency 60), 에디터는 Tools 메뉴로 켠다.
public static class LocalNetworkTest
{
    public enum Mode { None, Host, Client }

    public const string EditorModePrefKey = "FlipFriends.LocalNetworkTest.Mode";

    private const string HostArg = "-localhost";
    private const string ClientArg = "-localclient";
    private const string LatencyArg = "-latency";
    private const string LocalAddress = "localhost";

    // 양쪽 프로세스가 각자 송신을 늦추므로 왕복 지연은 이 값의 두 배다.
    private const float DefaultLatencyMs = 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        Mode mode = ReadMode();
        if (mode == Mode.None) return;

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null)
        {
            Debug.LogWarning("[LocalNetworkTest] NetworkManager가 없습니다. Main 씬에서 시작하세요.");
            return;
        }

        UseLocalTransport(manager, ReadLatency());
        // 매니저의 Start(Steam 매치메이킹 초기화)가 끝난 뒤 세션을 연다.
        manager.StartCoroutine(StartSessionNextFrame(manager, mode));
    }

    private static IEnumerator StartSessionNextFrame(NetworkManager manager, Mode mode)
    {
        yield return null;

        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[LocalNetworkTest] 이미 네트워크 세션이 실행 중이라 로컬 테스트를 건너뜁니다.");
            yield break;
        }

        if (mode == Mode.Host)
        {
            manager.StartHost();
        }
        else
        {
            manager.networkAddress = LocalAddress;
            manager.StartClient();
        }
        Debug.Log($"[LocalNetworkTest] {mode} 시작 (transport={Transport.active})");
    }

    private static void UseLocalTransport(NetworkManager manager, float latencyMs)
    {
        // LatencySimulation.Awake가 wrap을 요구하므로 비활성 상태에서 연결한 뒤 켠다.
        var transportObject = new GameObject("LocalTestTransport");
        transportObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(transportObject);

        var kcp = transportObject.AddComponent<KcpTransport>();
        var latency = transportObject.AddComponent<LatencySimulation>();
        latency.wrap = kcp;
        latency.latency = latencyMs;
        transportObject.SetActive(true);

        manager.transport = latency;
        Transport.active = latency;
    }

    private static Mode ReadMode()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, HostArg) >= 0) return Mode.Host;
        if (Array.IndexOf(args, ClientArg) >= 0) return Mode.Client;
#if UNITY_EDITOR
        return (Mode)UnityEditor.EditorPrefs.GetInt(EditorModePrefKey, (int)Mode.None);
#else
        return Mode.None;
#endif
    }

    private static float ReadLatency()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, LatencyArg);
        if (index >= 0 && index + 1 < args.Length && float.TryParse(args[index + 1], out float value))
            return Mathf.Max(0f, value);
        return DefaultLatencyMs;
    }
}
