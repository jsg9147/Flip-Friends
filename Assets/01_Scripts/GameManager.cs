using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    public GameObject menuScreen;

    private PlayerController2D[] playerControllers;
    private bool completionResultSent;

    public void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if(InputManager.instance != null)
        {
            InputManager.instance.OnMenuEvent += SetMenu;
        }
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
        {
            InputManager.instance.OnMenuEvent -= SetMenu;
        }
    }

    public void SetPlayerController(PlayerController2D[] playerControllers)
    {
        this.playerControllers = playerControllers;
    }

    public void FinishCheck()
    {
        if (!isServer || completionResultSent) return;

        playerControllers = FindObjectsByType<PlayerController2D>(
            FindObjectsSortMode.InstanceID);
        if (!TryGetTrustedCompletion(
                out SlimeRoomManager roomManager,
                out GameplayCompletionSession session,
                out MapCompletionTarget target))
            return;

        var finishedConnectionIds = new HashSet<int>();
        foreach (PlayerController2D player in playerControllers)
        {
            if (player == null || !player.isFinish || player.connectionToClient == null)
                continue;

            finishedConnectionIds.Add(player.connectionToClient.connectionId);
        }
        if (!session.AreAllParticipantsComplete(finishedConnectionIds))
            return;

        completionResultSent = true;
        SendCompletionToParticipants(session, target);
        roomManager.ReturnRoomScene();
    }

    [Server]
    private bool TryGetTrustedCompletion(
        out SlimeRoomManager roomManager,
        out GameplayCompletionSession session,
        out MapCompletionTarget target)
    {
        roomManager = NetworkManager.singleton as SlimeRoomManager;
        session = roomManager?.GameplayCompletionSession;
        target = default;
        if (roomManager == null || session == null || StageManager.instance == null)
            return false;

        if (StageManager.instance.TryGetLoadedCompletionTarget(out target))
            return true;

        Debug.LogError("서버가 실제 로드한 맵의 완료 리비전을 검증하지 못했습니다.");
        return false;
    }

    [Server]
    private void SendCompletionToParticipants(
        GameplayCompletionSession session,
        MapCompletionTarget target)
    {
        var notifiedConnections = new HashSet<int>();
        foreach (PlayerController2D player in playerControllers)
        {
            NetworkConnectionToClient connection = player?.connectionToClient;
            if (connection == null ||
                !session.ContainsParticipant(connection.connectionId) ||
                !notifiedConnections.Add(connection.connectionId))
                continue;

            int officialRevision = target.MapKey.Kind == LobbyMapKind.Official
                ? int.Parse(target.Revision)
                : 0;
            string contentHash = target.MapKey.Kind == LobbyMapKind.Custom
                ? target.Revision
                : string.Empty;
            TargetRecordMapCompletion(
                connection,
                target.MapKey.Kind,
                target.MapKey.MapId,
                officialRevision,
                contentHash);
        }
    }

    [TargetRpc]
    private void TargetRecordMapCompletion(
        NetworkConnectionToClient targetConnection,
        LobbyMapKind mapKind,
        string mapId,
        int officialRevision,
        string contentHash)
    {
        MapCompletionTarget completion;
        bool valid = mapKind switch
        {
            LobbyMapKind.Official => MapCompletionTarget.TryCreateOfficial(
                mapId, officialRevision, out completion),
            LobbyMapKind.Custom => MapCompletionTarget.TryCreateCustom(
                mapId, contentHash, out completion),
            _ => FailCompletionTarget(out completion)
        };
        if (!valid)
        {
            Debug.LogError(
                $"서버에서 받은 맵 완료 결과가 올바르지 않습니다: " +
                $"kind={mapKind}, mapId={mapId}");
            return;
        }

        MapCompletionProgress.TryRecord(completion);
    }

    private static bool FailCompletionTarget(out MapCompletionTarget target)
    {
        target = default;
        return false;
    }

    public void ExitGame()
    {
        if (SteamRoomManager.Instance != null)
        {
            SteamRoomManager.Instance.LeaveLobby();
        }
        NetworkManager.singleton.StopClient();
    }

    private void SetMenu()
    {
        menuScreen.SetActive(!menuScreen.activeSelf);
    }
}
