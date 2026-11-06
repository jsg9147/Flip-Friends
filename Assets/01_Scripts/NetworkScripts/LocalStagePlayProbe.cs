#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

// 명시적으로 지정한 개발 빌드에서만 소켓·씬·물리 검증 자료를 수집한다.
public sealed class LocalStagePlayProbe : MonoBehaviour
{
    [Serializable]
    private sealed class Command
    {
        public int sequence;
        public string action;
        public string stage;
        public int count;
    }

    private string outputPath;
    private int lastSequence;
    private float nextSample;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-stageprobe");
        if (index < 0 || index + 1 >= args.Length) return;
        var probe = new GameObject("LocalStagePlayProbe").AddComponent<LocalStagePlayProbe>();
        probe.outputPath = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(Path.GetDirectoryName(probe.outputPath));
        DontDestroyOnLoad(probe.gameObject);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSample) return;
        nextSample = Time.unscaledTime + .1f;
        try
        {
            string commandPath = outputPath + ".command";
            if (File.Exists(commandPath))
            {
                Command command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath));
                if (command.sequence > lastSequence)
                {
                    lastSequence = command.sequence;
                    Execute(command);
                }
            }
            Sample();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            enabled = false;
        }
    }

    private void Execute(Command command)
    {
        if (command.action == "quit") { Application.Quit(); return; }
        if (command.action == "disconnect") { NetworkManager.singleton.StopClient(); return; }
        if (!NetworkServer.active) throw new InvalidOperationException("서버 전용 검증 명령입니다.");
        var manager = (SlimeRoomManager)NetworkManager.singleton;
        switch (command.action)
        {
            case "stage":
                manager.currentMapId = string.Empty;
                manager.currentBuiltInMapId = command.stage;
                manager.ServerChangeScene(manager.GameplayScene);
                break;
            case "finish":
                foreach (var player in FindObjectsByType<PlayerController2D>().OrderBy(item => item.netId).Take(command.count))
                    player.isFinish = true;
                GameManager.Instance.FinishCheck();
                break;
            case "room": manager.ReturnRoomScene(); break;
            case "cannonsOff":
            case "cannonsOn":
                foreach (var cannon in FindObjectsByType<CannonShooter>())
                    cannon.enabled = command.action == "cannonsOn";
                break;
            case "dynamicRepeat":
                foreach (var item in FindObjectsByType<DynamicObj>())
                {
                    item.ServerApplyPlayerCount(manager.numPlayers);
                    item.ServerApplyPlayerCount(manager.numPlayers);
                }
                break;
            default: throw new InvalidOperationException("알 수 없는 검증 명령: " + command.action);
        }
        Debug.Log("[StagePlayProbe] API " + JsonUtility.ToJson(command));
    }

    private void Sample()
    {
        var identities = NetworkServer.active ? NetworkServer.spawned.Values : NetworkClient.spawned.Values;
        var snapshot = new
        {
            time = NetworkTime.time,
            realtime = Time.realtimeSinceStartupAsDouble,
            scene = SceneManager.GetActiveScene().name,
            server = NetworkServer.active,
            connected = NetworkClient.isConnected,
            connections = NetworkServer.connections.Count,
            command = lastSequence,
            objects = identities.Where(item => item != null).Select(item => new
            {
                id = item.netId, name = item.name,
                position = new[] { item.transform.position.x, item.transform.position.y },
                identities = item.GetComponentsInChildren<NetworkIdentity>(true).Length
            }).ToArray(),
            players = FindObjectsByType<PlayerController2D>().Select(item => new
            {
                id = item.netId, finish = item.isFinish,
                position = new[] { item.transform.position.x, item.transform.position.y }
            }).ToArray(),
            dynamicObjects = FindObjectsByType<DynamicObj>().Select(item => new
            {
                position = new[] { item.transform.position.x, item.transform.position.y },
                offset = new[] { item.xMoveValue, item.yMoveValue }
            }).ToArray(),
            bullets = FindObjectsByType<CannonProjectile>().Select(item => new
            {
                id = item.netId,
                position = new[] { item.transform.position.x, item.transform.position.y },
                velocity = new[] { item.LaunchVelocity.x, item.LaunchVelocity.y },
                simulated = item.GetComponent<Rigidbody2D>().simulated
            }).ToArray(),
            cannons = FindObjectsByType<CannonShooter>().Select(item => new { item.IsScheduled, item.NextShootTime }).ToArray()
        };
        string json = JsonConvert.SerializeObject(snapshot);
        File.WriteAllText(outputPath + ".latest", json);
        File.AppendAllText(outputPath + ".jsonl", json + Environment.NewLine);
    }
}
#endif
