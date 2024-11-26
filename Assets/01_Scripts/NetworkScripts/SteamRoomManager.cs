using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SteamRoomManager : SlimeRoomManager
{
    public static SteamRoomManager Instance { get; private set; }

    public int maxLobbyMembers = 4;
    public List<SteamLobbyInfo> lobbyInfos = new List<SteamLobbyInfo>();
    public string lobbyKeyStr { get; private set; }

    private CSteamID mySteamID;
    public CSteamID currentLobbyID { get; private set; }
    private List<CSteamID> lobbyIDs = new List<CSteamID>();

    private const string HostAddressKey = "FlipFriends";
    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<LobbyMatchList_t> lobbyMatchList;

    public string playerName { get; private set; }
    public GameObject temp;

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("중복된 SteamRoomManager가 감지되어 파괴됩니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI 초기화 실패");
            temp.SetActive(true);
            return;
        }

        base.Awake();
    }

    public override void Start()
    {
        base.Start();
        InitializeSteamCallbacks();
        mySteamID = SteamUser.GetSteamID();
        playerName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
    }

    private void InitializeSteamCallbacks()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public override void StartHosting()
    {
        base.StartHosting();
        HostLobby();
    }

    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxLobbyMembers);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        SteamMatchmaking.RequestLobbyList();
        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
        {
            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
                string existingKey = SteamMatchmaking.GetLobbyData(lobbyID, HostAddressKey);

                if (existingKey == joinCode)
                {
                    SteamMatchmaking.JoinLobby(lobbyID);
                    lobbyKeyStr = joinCode;
                    return;
                }
            }
            Debug.LogWarning("일치하는 로비를 찾을 수 없습니다.");
        });
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) { return; }
        lobbyKeyStr = GenerateUniqueLobbyKey();
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        string playerSteamName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());
        SteamMatchmaking.SetLobbyData(currentLobbyID, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(currentLobbyID, PrivateLobbyKey, lobbyKeyStr);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "Name", playerSteamName);
        SteamMatchmaking.SetLobbyData(currentLobbyID, "Color", "Green");
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (NetworkServer.active) { return; }

        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);

        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out _))
        {
            Debug.LogError("유효하지 않은 호스트 주소입니다. Steam ID를 확인하세요.");
            return;
        }

        networkAddress = hostAddress;
        StartClient();
    }

    private string GenerateUniqueLobbyKey(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] data = new byte[length];
            rng.GetBytes(data);
            StringBuilder result = new StringBuilder(length);
            foreach (byte b in data)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        lobbyIDs.Clear();
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            lobbyIDs.Add(lobbyID);
        }
    }

    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            StopHost();
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            Debug.Log("로비를 나갔습니다: " + currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
        else
        {
            Debug.LogWarning("현재 참가 중인 로비가 없습니다.");
        }
    }

    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        lobbyInfos.Clear();
        var tcs = new TaskCompletionSource<List<SteamLobbyInfo>>();

        SteamMatchmaking.RequestLobbyList();

        Callback<LobbyMatchList_t> lobbyMatchList = null;
        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
        {
            try
            {
                for (int i = 0; i < callback.m_nLobbiesMatching; i++)
                {
                    SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(SteamMatchmaking.GetLobbyByIndex(i));
                    string existingKey = SteamMatchmaking.GetLobbyData(lobbyInfo.LobbyID, HostAddressKey);
                    if (existingKey != "")
                    {
                        lobbyInfos.Add(lobbyInfo);
                    }
                }

                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(lobbyInfos);
                }
            }
            catch (Exception ex)
            {
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetException(ex);
                }
            }
            finally
            {
                lobbyMatchList?.Dispose();
            }
        });

        return await tcs.Task;
    }

    public void JoinLobby(CSteamID joinID)
    {
        // 모든 로비를 요청
        SteamMatchmaking.RequestLobbyList();
        SteamMatchmaking.JoinLobby(joinID);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Instance = null;
    }

    public override void ReturnRoomScene()
    {
        base.ReturnRoomScene();
    }
}
