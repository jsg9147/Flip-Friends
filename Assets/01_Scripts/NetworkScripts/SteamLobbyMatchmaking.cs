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
public sealed class SteamLobbyMatchmaking : IDisposable
{
    internal const string QuantumModeKey = "ff.mode";
    internal const string QuantumMode = "quantum-v1";
    private const string HostAddressKey = "FlipFriends";
    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";
    private const string RoomTypeKey = "FlipFriendsRoomType";
    private const string LobbyNameKey = "Name";
    private const float LobbyListTimeoutSeconds = 10f;
    private const int LobbyKeyLength = 8;

    public string LobbyKey { get; private set; }
    public CSteamID CurrentLobbyId { get; private set; }
    public List<SteamLobbyInfo> LobbyInfos { get; } = new List<SteamLobbyInfo>();

    public event Action LobbyCreateFailed;
    public event Action LobbyJoinFailed;
    public event Action<CSteamID, string> LobbyEntered;

    private RoomType pendingRoomType;
    private bool isCreating;
    private bool isJoining;
    private bool isCreatingQuantum;
    public bool IsPending => isCreating || isJoining || isFindingQuantumLobby;
    private bool isFindingQuantumLobby;
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

    public void CreateLobby(RoomType roomType, int maxPlayer, bool isQuantum = false)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isCreating = true;
        isCreatingQuantum = isQuantum;
        pendingRoomType = roomType;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayer);
    }

    public void JoinLobby(CSteamID joinId)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isJoining = true;
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
            LobbyJoinFailed?.Invoke();
        });
    }

    public async Task JoinQuantumLobbyByCodeAsync(string code)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isFindingQuantumLobby = true;
        var match = new TaskCompletionSource<CSteamID>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var result = CallResult<LobbyMatchList_t>.Create((value, failed) =>
            match.TrySetResult(!failed && value.m_nLobbiesMatching > 0 ? SteamMatchmaking.GetLobbyByIndex(0) : CSteamID.Nil));
        CSteamID target = CSteamID.Nil;
        try
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter(QuantumModeKey, QuantumMode, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(PrivateLobbyKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            result.Set(SteamMatchmaking.RequestLobbyList());
            if (await Task.WhenAny(match.Task, Task.Delay(TimeSpan.FromSeconds(LobbyListTimeoutSeconds))) == match.Task)
                target = await match.Task;
        }
        finally
        {
            isFindingQuantumLobby = false;
        }

        if (target == CSteamID.Nil) LobbyJoinFailed?.Invoke();
        else JoinLobby(target);
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
        LobbyKey = null;
    }

    // 로비 소유자만 바꿀 수 있다. 입장 불가 로비는 목록 검색과 초대 입장에서 모두 빠진다.
    public void SetLobbyJoinable(bool isJoinable)
    {
        if (CurrentLobbyId == CSteamID.Nil)
            return;

        if (!SteamMatchmaking.SetLobbyJoinable(CurrentLobbyId, isJoinable))
            Debug.LogWarning($"로비 입장 가능 여부를 바꾸지 못했습니다: lobby={CurrentLobbyId}, joinable={isJoinable}");
    }

    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        LobbyInfos.Clear();
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
        if (!isCreating) return;
        isCreating = false;
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

        if (isCreatingQuantum)
        {
            if (!SteamMatchmaking.SetLobbyData(CurrentLobbyId, QuantumModeKey, QuantumMode))
            {
                LeaveLobby();
                LobbyCreateFailed?.Invoke();
                return;
            }
        }
        else SteamMatchmaking.SetLobbyData(CurrentLobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, RoomTypeKey, pendingRoomType.ToString());
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, PrivateLobbyKey, LobbyKey);
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, LobbyNameKey, playerSteamName);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        var enteredId = new CSteamID(callback.m_ulSteamIDLobby);
        if (!isJoining && enteredId != CurrentLobbyId) return;
        isJoining = false;
        if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            LobbyJoinFailed?.Invoke();
            return;
        }
        CurrentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        LobbyKey = SteamMatchmaking.GetLobbyData(CurrentLobbyId, PrivateLobbyKey);
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

            JoinLobby(lobbyId);
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

            if (roomTypeValue == RoomType.Public.ToString())
            {
                LobbyInfos.Add(lobbyInfo);
            }
        }

        return LobbyInfos;
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

    public void Dispose()
    {
        lobbyCreated?.Dispose();
        gameLobbyJoinRequested?.Dispose();
        lobbyEntered?.Dispose();
        lobbyMatchList?.Dispose();
        pendingLobbyMatchHandler = null;
    }
}
