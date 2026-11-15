using Steamworks;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Quantum용 Steam 로비 생성/참가/코드 검색만 담당한다.
/// </summary>
public sealed class SteamLobbyMatchmaking : IDisposable
{
    internal const string QuantumModeKey = "ff.mode";
    internal const string QuantumMode = "quantum-v1";
    private const string LobbyCodeKey = "FlipFriendsLobbyKey";
    private const string LobbyNameKey = "Name";
    private const float LobbyListTimeoutSeconds = 10f;
    private const int LobbyKeyLength = 8;

    public string LobbyKey { get; private set; }
    public CSteamID CurrentLobbyId { get; private set; }

    public event Action LobbyCreateFailed;
    public event Action LobbyJoinFailed;
    public event Action<CSteamID> LobbyEntered;

    private bool isCreating;
    private bool isJoining;
    private bool isFindingLobby;
    public bool IsPending => isCreating || isJoining || isFindingLobby;

    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    private Callback<LobbyEnter_t> lobbyEntered;

    public void Initialize()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    // 코드 검색이 목록 조회로 로비를 찾으므로 비공개 방도 Steam에서는 공개 로비로 만든다. 코드를 모르면 찾을 수 없다.
    public void CreateLobby(int maxPlayer)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isCreating = true;
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayer);
    }

    public void JoinLobby(CSteamID joinId)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isJoining = true;
        SteamMatchmaking.JoinLobby(joinId);
    }

    public async Task JoinQuantumLobbyByCodeAsync(string code)
    {
        if (IsPending || CurrentLobbyId != CSteamID.Nil) return;
        isFindingLobby = true;
        var match = new TaskCompletionSource<CSteamID>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var result = CallResult<LobbyMatchList_t>.Create((value, failed) =>
            match.TrySetResult(!failed && value.m_nLobbiesMatching > 0 ? SteamMatchmaking.GetLobbyByIndex(0) : CSteamID.Nil));
        CSteamID target = CSteamID.Nil;
        try
        {
            SteamMatchmaking.AddRequestLobbyListStringFilter(QuantumModeKey, QuantumMode, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyCodeKey, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            result.Set(SteamMatchmaking.RequestLobbyList());
            if (await Task.WhenAny(match.Task, Task.Delay(TimeSpan.FromSeconds(LobbyListTimeoutSeconds))) == match.Task)
                target = await match.Task;
        }
        finally
        {
            isFindingLobby = false;
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

        if (!SteamMatchmaking.SetLobbyData(CurrentLobbyId, QuantumModeKey, QuantumMode))
        {
            LeaveLobby();
            LobbyCreateFailed?.Invoke();
            return;
        }
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, LobbyCodeKey, LobbyKey);
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, LobbyNameKey, SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID()));
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
        CurrentLobbyId = enteredId;
        LobbyKey = SteamMatchmaking.GetLobbyData(CurrentLobbyId, LobbyCodeKey);
        LobbyEntered?.Invoke(CurrentLobbyId);
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
    }
}
