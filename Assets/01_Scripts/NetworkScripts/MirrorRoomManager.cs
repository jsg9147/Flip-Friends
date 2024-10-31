using UnityEngine;
using Mirror;
using Steamworks;
using Mirror.FizzySteam;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Edgegap;
using System.Linq;

public class MirrorRoomManager : NetworkRoomManager
{
    public static MirrorRoomManager Instance { get; private set; }

    public int maxLobbyMembers = 4;
    public List<SteamLobbyInfo> lobbyInfos = new List<SteamLobbyInfo>();
    public string lobbyKeyStr { get; private set; }

    private CSteamID mySteamID;
    public CSteamID currentLobbyID { get; private set; }
    private List<CSteamID> lobbyIDs = new List<CSteamID>();

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<LobbyMatchList_t> lobbyMatchList;

    private const string HostAddressKey = "FlipFriends";
    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";

    public string playerName;
    public int currentStage = 0;

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("중복된 MirrorNetworkManager가 감지되어 파괴됩니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI 초기화 실패");
            return;
        }

        base.Awake();
    }

    public override void Start()
    {
        transport = FindAnyObjectByType<FizzySteamworks>();

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);

        mySteamID = SteamUser.GetSteamID();

        //NetworkManager.Instance.SetMirrorRoomManager(this);
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public void StartHosting()
    {
        StartHost();
        HostLobby();
    }

    public void StartJoining(string networkAddress)
    {
        this.networkAddress = networkAddress;
        StartClient();
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
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), PrivateLobbyKey, lobbyKeyStr);
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "Name", playerSteamName);
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "Color", "Green");
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

        // Steam ID 형식이 아닌 경우 에러 처리
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
        // 기존의 로비 정보 리스트를 비웁니다.
        lobbyInfos.Clear();

        // 비동기 작업을 위한 TaskCompletionSource를 생성합니다.
        var tcs = new TaskCompletionSource<List<SteamLobbyInfo>>();

        // 로비 목록을 요청합니다.
        SteamMatchmaking.RequestLobbyList();

        // Callback을 통해 로비 목록이 수신되면 처리합니다.
        Callback<LobbyMatchList_t> lobbyMatchList = null;

        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
        {
            try
            {
                // 로비 정보를 수신 후 리스트에 추가
                for (int i = 0; i < callback.m_nLobbiesMatching; i++)
                {
                    SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(SteamMatchmaking.GetLobbyByIndex(i));
                    string existingKey = SteamMatchmaking.GetLobbyData(lobbyInfo.LobbyID, HostAddressKey);
                    if (existingKey != "")
                    {
                        lobbyInfos.Add(lobbyInfo);
                    }
                }

                // Task가 이미 완료되지 않았다면 결과 설정
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetResult(lobbyInfos);
                }
            }
            catch (Exception ex)
            {
                // 에러 발생 시 Task를 실패 상태로 설정
                if (!tcs.Task.IsCompleted)
                {
                    tcs.SetException(ex);
                }
            }
            finally
            {
                // 사용 후 콜백 해제
                lobbyMatchList?.Dispose();
            }
        });

        // TaskCompletionSource 결과 반환
        return await tcs.Task;
    }

    public void JoinLobby(CSteamID joinID)
    {
        // 모든 로비를 요청
        SteamMatchmaking.RequestLobbyList();
        SteamMatchmaking.JoinLobby(joinID);
    }

    public override void ReadyStatusChanged()
    {
        base.ReadyStatusChanged();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Instance = null;
    }

    public bool CheckAllPlayersReady()
    {
        bool allReady = roomSlots.All(player => player is CustomRoomPlayer customPlayer && customPlayer.isReady);

        return allReady;
    }
}
