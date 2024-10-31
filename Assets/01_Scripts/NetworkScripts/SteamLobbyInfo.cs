using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class SteamLobbyInfo
{
    // 로비의 Steam ID
    public CSteamID LobbyID { get; private set; }

    // 로비의 이름
    public string LobbyName { get; private set; }

    // 로비의 최대 인원 수
    public int MaxMembers { get; private set; }

    // 현재 로비에 접속한 멤버 수
    public int CurrentMemberCount { get; private set; }

    // 로비에 있는 멤버들의 Steam ID 목록
    public List<CSteamID> MemberIDs { get; private set; }

    // 로비가 게임 중인지 여부
    public bool IsInGame { get; private set; }

    public string colorStr { get; private set; }

    // 생성자
    public SteamLobbyInfo(CSteamID lobbyID)
    {
        LobbyID = lobbyID;
        UpdateLobbyInfo();
    }

    // 로비 정보 업데이트
    public void UpdateLobbyInfo()
    {
        if (!SteamManager.Initialized)
        {
            throw new Exception("Steam is not initialized.");
        }

        LobbyName = SteamMatchmaking.GetLobbyData(LobbyID, "Name");
        colorStr = SteamMatchmaking.GetLobbyData(LobbyID, "Color");
        MaxMembers = SteamMatchmaking.GetLobbyMemberLimit(LobbyID);
        CurrentMemberCount = SteamMatchmaking.GetNumLobbyMembers(LobbyID);
        IsInGame = SteamMatchmaking.GetLobbyData(LobbyID, "status") == "in_game";
        MemberIDs = new List<CSteamID>();
        for (int i = 0; i < CurrentMemberCount; i++)
        {
            MemberIDs.Add(SteamMatchmaking.GetLobbyMemberByIndex(LobbyID, i));
        }
    }

    // 멤버들의 이름 목록 가져오기
    public List<string> GetMemberNames()
    {
        List<string> memberNames = new List<string>();
        foreach (CSteamID memberID in MemberIDs)
        {
            memberNames.Add(SteamFriends.GetFriendPersonaName(memberID));
        }
        return memberNames;
    }
}