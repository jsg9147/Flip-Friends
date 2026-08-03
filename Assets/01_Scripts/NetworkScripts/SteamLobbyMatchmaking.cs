using Steamworks;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Steam 로비 생성/참가/목록 조회만 담당한다.
/// </summary>
public sealed class SteamLobbyMatchmaking
{
    private const string HostAddressKey = "FlipFriends";
    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";
    private const string RoomTypeKey = "FlipFriendsRoomType";
    private const string LobbyNameKey = "Name";
    public const string MapPolicyKey = "FFMapPolicy";
    public const string MapKindKey = "FFMapKind";
    public const string MapIdKey = "FFMapId";
    public const string MapNameKey = "FFMapName";
    public const string MapAuthorKey = "FFMapAuthor";
    public const string MapVersionKey = "FFMapVersion";
    public const string MapMinimumPlayersKey = "FFMapMinPlayers";
    private const float LobbyListTimeoutSeconds = 10f;
    private const int LobbyKeyLength = 8;

    public string LobbyKey { get; private set; }
    public CSteamID CurrentLobbyId { get; private set; }
    public List<SteamLobbyInfo> LobbyInfos { get; } = new List<SteamLobbyInfo>();

    public event Action LobbyCreateFailed;
    public event Action<CSteamID, string> LobbyEntered;

    private RoomType pendingRoomType;
    private LobbyMapMetadata pendingMapMetadata;
    private LobbyMapFilter pendingLobbyFilter = LobbyMapFilter.All;
    private Action<LobbyMatchList_t> pendingLobbyMatchHandler;

    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    private Callback<LobbyEnter_t> lobbyEntered;
    private Callback<LobbyMatchList_t> lobbyMatchList;

    public void Initialize()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    public bool CreateLobby(
        RoomType roomType,
        int maxPlayer,
        LobbyMapMetadata mapMetadata,
        MapCompletionTarget completionTarget)
    {
        RoomCreationEligibility eligibility =
            MapCompletionProgress.EvaluateRoomCreation(
                roomType == RoomType.Public,
                maxPlayer,
                mapMetadata,
                completionTarget);
        if (!eligibility.CanCreate)
        {
            Debug.LogWarning(
                $"Steam 로비 생성 진입점에서 요청을 거부했습니다: " +
                $"reason={eligibility.BlockReason}, roomType={roomType}, " +
                $"map={mapMetadata?.MapKey}");
            return false;
        }

        pendingRoomType = roomType;
        pendingMapMetadata = mapMetadata;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayer);
        return true;
    }

    public void JoinLobby(CSteamID joinId)
    {
        SteamMatchmaking.JoinLobby(joinId);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        RequestLobbyList(callback =>
        {
            if (TryJoinLobbyByKey(callback, joinCode))
            {
                return;
            }

            Debug.LogWarning("매칭되는 로비를 찾을 수 없습니다.");
        });
    }

    public void LeaveLobby()
    {
        if (CurrentLobbyId == CSteamID.Nil)
        {
            Debug.LogWarning("현재 참여 중인 로비가 없습니다.");
            return;
        }

        SteamMatchmaking.LeaveLobby(CurrentLobbyId);
        Debug.Log("로비를 떠납니다: " + CurrentLobbyId);
        CurrentLobbyId = CSteamID.Nil;
    }

    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync(
        LobbyMapFilter? filter = null)
    {
        LobbyInfos.Clear();
        pendingLobbyFilter = filter ?? LobbyMapFilter.All;
        var tcs = new TaskCompletionSource<List<SteamLobbyInfo>>();
        Action<LobbyMatchList_t> handler = CreateLobbyListHandler(tcs);

        RequestLobbyList(handler);

        Task completedTask = await Task.WhenAny(
            tcs.Task,
            Task.Delay(TimeSpan.FromSeconds(LobbyListTimeoutSeconds)));

        if (completedTask == tcs.Task)
        {
            return await tcs.Task;
        }

        CancelPendingLobbyMatchHandler(handler);
        Debug.LogWarning("로비 목록 요청이 시간 초과되었습니다.");
        throw new TimeoutException("로비 목록 요청이 시간 초과되었습니다.");
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Steam 로비 생성 실패: {callback.m_eResult}");
            LobbyCreateFailed?.Invoke();
            return;
        }

        ApplyCreatedLobbyData(new CSteamID(callback.m_ulSteamIDLobby));
    }

    private void ApplyCreatedLobbyData(CSteamID lobbyId)
    {
        LobbyKey = GenerateLobbyKey();
        CurrentLobbyId = lobbyId;

        string playerSteamName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());

        // 클라이언트가 LobbyEntered에서 연결하려면 항상 호스트 주소가 필요
        bool stored =
            SteamMatchmaking.SetLobbyData(
                CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString()) &
            SteamMatchmaking.SetLobbyData(
                CurrentLobbyId, RoomTypeKey, pendingRoomType.ToString()) &
            SteamMatchmaking.SetLobbyData(
                CurrentLobbyId, PrivateLobbyKey, LobbyKey) &
            SteamMatchmaking.SetLobbyData(
                CurrentLobbyId, LobbyNameKey, playerSteamName) &
            TrySetMapLobbyData(CurrentLobbyId, pendingMapMetadata);
        if (stored) return;

        Debug.LogError("Steam 로비 필수 메타데이터 저장에 실패했습니다.");
        SteamMatchmaking.LeaveLobby(CurrentLobbyId);
        CurrentLobbyId = CSteamID.Nil;
        LobbyCreateFailed?.Invoke();
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamMatchmaking.GetLobbyData(CurrentLobbyId, HostAddressKey);
        LobbyEntered?.Invoke(CurrentLobbyId, hostAddress);
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        Action<LobbyMatchList_t> handler = pendingLobbyMatchHandler;
        pendingLobbyMatchHandler = null;
        handler?.Invoke(callback);
    }

    private void RequestLobbyList(Action<LobbyMatchList_t> onComplete)
    {
        if (pendingLobbyMatchHandler != null)
        {
            Debug.LogWarning("진행 중인 로비 목록 요청을 새 요청으로 대체합니다.");
        }

        pendingLobbyMatchHandler = onComplete;
        SteamMatchmaking.RequestLobbyList();
    }

    private bool TryJoinLobbyByKey(LobbyMatchList_t callback, string joinCode)
    {
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
            string existingKey = SteamMatchmaking.GetLobbyData(lobbyId, PrivateLobbyKey);

            if (existingKey != joinCode)
            {
                continue;
            }

            SteamMatchmaking.JoinLobby(lobbyId);
            LobbyKey = joinCode;
            return true;
        }

        return false;
    }

    private Action<LobbyMatchList_t> CreateLobbyListHandler(
        TaskCompletionSource<List<SteamLobbyInfo>> tcs)
    {
        return callback =>
        {
            try
            {
                tcs.TrySetResult(CollectPublicLobbies(callback));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };
    }

    private List<SteamLobbyInfo> CollectPublicLobbies(LobbyMatchList_t callback)
    {
        LobbyInfos.Clear();

        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(SteamMatchmaking.GetLobbyByIndex(i));
            string roomTypeValue = SteamMatchmaking.GetLobbyData(lobbyInfo.LobbyID, RoomTypeKey);

            if (roomTypeValue == RoomType.Public.ToString() &&
                lobbyInfo.MapMetadata != null &&
                RoomCreationRules.HasEnoughCapacity(
                    lobbyInfo.MaxMembers,
                    lobbyInfo.MapMetadata.MinimumPlayersToClear) &&
                pendingLobbyFilter.Matches(lobbyInfo.MapMetadata))
            {
                LobbyInfos.Add(lobbyInfo);
            }
        }

        return LobbyInfos;
    }

    private static bool TrySetMapLobbyData(
        CSteamID lobbyId,
        LobbyMapMetadata metadata)
    {
        if (metadata == null)
        {
            Debug.LogError("Steam 로비에 기록할 맵 메타데이터가 없습니다.");
            return false;
        }

        return
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapPolicyKey, metadata.Policy.ToString()) &
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapKindKey, metadata.MapKey.Kind.ToString()) &
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapIdKey, metadata.MapKey.MapId) &
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapNameKey, metadata.DisplayName) &
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapAuthorKey, metadata.AuthorName) &
            SteamMatchmaking.SetLobbyData(
                lobbyId, MapVersionKey, metadata.Version) &
            SteamMatchmaking.SetLobbyData(
                lobbyId,
                MapMinimumPlayersKey,
                metadata.MinimumPlayersToClear.ToString());
    }

    private void CancelPendingLobbyMatchHandler(Action<LobbyMatchList_t> handler)
    {
        if (pendingLobbyMatchHandler == handler)
        {
            pendingLobbyMatchHandler = null;
        }
    }

    private static string GenerateLobbyKey()
    {
        const string chars = "0123456789";
        byte[] data = new byte[LobbyKeyLength];
        RandomNumberGenerator.Fill(data);

        StringBuilder result = new StringBuilder(LobbyKeyLength);
        foreach (byte value in data)
        {
            result.Append(chars[value % chars.Length]);
        }

        return result.ToString();
    }
}
