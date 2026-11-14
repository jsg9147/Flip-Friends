using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using FlipFriends.Network;
using Photon.Realtime;
using Quantum;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class QuantumLobbyChecks
{
    [MenuItem("Tools/Flip Friends/Quantum/Check Steam Lobby With Development Photon")]
    public static async void Run()
    {
        var evidence = new List<string>();
        var flow = QuantumGameFlow.Instance;
        GuestGame guest = null;
        try
        {
            Require(Application.isPlaying && SceneManager.GetActiveScene().name == "Main" && flow != null &&
                !flow.IsBusy && flow.Lobby == null, "RunOffline 이후 유휴 Main에서 실행");
            Require(!flow.CheckLobbyRelease() && flow.Session == null, "출시 설정 누락 차단");
            evidence.Add("출시 설정 누락 차단");

            var stage = flow.OfficialStages[0];
            var palette = AssetDatabase.LoadAssetAtPath<MapEditorPalette>("Assets/07_ScriptableObject/MapEditorPalette.asset");
            var data = MapDataRepository.FromJson(Resources.Load<TextAsset>("FlipFriendsCustomMap").text);
            data.mapId = Guid.NewGuid().ToString("N");
            data.mapName = "로비 전송 검사";
            Require(MapSessionSnapshot.TryCreate(0, MapDataRepository.ToJson(data), MapSessionSnapshot.MaximumContentBytes,
                palette, out var snapshot, out var error), "저장 맵 스냅샷: " + error);

            var store = new LobbyStore();
            double now = 0;
            flow.AttachLobby(new LobbyEndpoint(store, "host"), isDevelopment: true, clock: () => now);
            guest = new GuestGame(stage, palette);
            var visitor = new QuantumLobby(new LobbyEndpoint(store, "guest"), guest, isDevelopment: true, clock: () => now);
            var host = flow.Lobby;
            host.Select("official:" + stage.StageId, stage.DisplayName);
            visitor.Tick();
            host.ToggleReady();
            host.Start();
            Require(store.Joinable && flow.Session == null, "준비 미완료 시작 거부");
            visitor.ToggleReady();
            visitor.ToggleReady();
            host.Start();
            Require(store.Joinable && flow.Session == null, "준비 해제 후 시작 거부");
            evidence.Add("Steam 모의: 준비 미완료·준비 해제 거부");

            visitor.ToggleReady();
            host.Start();
            string attempt = QuantumLobbyState.Read(store.State).Attempt;
            host.Start();
            Require(!store.Joinable && attempt == QuantumLobbyState.Read(store.State).Attempt, "입장 잠금·중복 시작");
            await host.ReturnAsync("검사: 시작 취소");
            visitor.Tick();
            Require(store.Joinable && !host.IsReady && !visitor.IsReady && flow.Session == null, "시작 취소 후 초기화");
            evidence.Add("Steam 모의: 입장 잠금·중복 시작·시작 취소·준비 초기화");

            StartBoth(host, visitor);
            now += 76;
            host.Tick();
            await WaitUntil(() => store.Joinable && flow.Session == null, "로비 제한 시간");
            visitor.Tick();
            evidence.Add("Steam 모의: 제한 시간 후 로비 복구");

            guest.Mismatch = true;
            StartBoth(host, visitor);
            await WaitUntil(() =>
            {
                visitor.Tick();
                return store.Joinable && !guest.IsBusy && flow.Session == null;
            }, "콘텐츠 불일치 복구");
            Require(guest.LastStartError?.Contains("해시") == true || flow.Message.Contains("해시"),
                $"콘텐츠 불일치 사유: {guest.LastStartError} / {flow.Message}");
            guest.Mismatch = false;
            evidence.Add("Photon 실제: 공식 콘텐츠 불일치 뒤 복구");

            await RoundTrip(host, visitor, flow, guest, store);
            evidence.Add("Photon 실제: 공식 맵 두 연결·QuantumPlay·메뉴 복귀·정리");
            host.Select("saved:" + snapshot.ContentHash, data.mapName, snapshot);
            await RoundTrip(host, visitor, flow, guest, store);
            evidence.Add("Photon 실제: 저장 맵 미보유 게스트 전송·두 연결·복귀·정리");
            await RoundTrip(host, visitor, flow, guest, store);
            evidence.Add("Photon 실제: 같은 저장 맵으로 재시작");

            StartBoth(host, visitor);
            visitor.Leave("검사: 참가자 이탈");
            host.Tick();
            await WaitUntil(() => store.Joinable && flow.Session == null, "참가자 이탈");
            Require(!host.IsReady && !guest.IsBusy, "이탈 후 준비 초기화");
            store.Members = new[] { "guest", "host" };
            host.Tick();
            visitor = new QuantumLobby(new LobbyEndpoint(store, "guest"), guest, isDevelopment: true);
            StartBoth(host, visitor);
            host.Leave("검사: 방장 이탈");
            visitor.Tick();
            await WaitUntil(() =>
            {
                visitor.Tick();
                return flow.Lobby == null && store.Joinable && !guest.IsBusy;
            }, "방장 이탈");
            evidence.Add("Steam 모의: 참가자 이탈 복구·방장 이탈 후 새 방장의 로비 복구");

            flow.AttachLobby(new LobbyEndpoint(store, "guest"), isDevelopment: true);
            store.Members = Array.Empty<string>();
            flow.Lobby.Tick();
            await WaitUntil(() => flow.Lobby == null, "로비 소멸");
            Require(SceneManager.GetActiveScene().name == "Main" && flow.Message.Contains("사라졌"), "Main 복귀·소멸 사유");
            evidence.Add("Steam 모의: 로비 소멸과 Main 안내");
            evidence.Add("PASS");
        }
        catch (Exception error)
        {
            evidence.Add("FAIL " + error);
            Debug.LogException(error);
        }
        finally
        {
            flow?.Lobby?.Leave("로비 검사 종료");
            if (guest != null) await guest.ReturnFromLobbyAsync("로비 검사 종료");
            File.WriteAllLines("Logs/QuantumSteamLobbyChecks.txt", evidence);
        }
    }

    private static void StartBoth(QuantumLobby host, QuantumLobby visitor)
    {
        visitor.Tick();
        host.ToggleReady();
        visitor.ToggleReady();
        Require(host.CanStart, "전원 준비");
        host.Start();
        visitor.Tick();
    }

    private static async Task RoundTrip(QuantumLobby host, QuantumLobby visitor, QuantumGameFlow flow, GuestGame guest, LobbyStore store)
    {
        StartBoth(host, visitor);
        await WaitUntil(() =>
        {
            visitor.Tick();
            return flow.State == QuantumGameState.Playing && guest.State == QuantumGameState.Playing;
        }, "두 연결 Quantum 시작");
        var sessions = new[] { flow.Session, guest.Session };
        var clients = sessions.Select(session => session.Client).ToArray();
        Require(SceneManager.GetActiveScene().name == QuantumGameFlow.PlayScene && !store.Joinable &&
            clients.All(client => client.CurrentRoom.PlayerCount == 2 && !client.CurrentRoom.IsOpen), "QuantumPlay·Photon 입장 잠금");
        await WaitUntil(() => sessions.All(session => session.Runner.Game.Frames.Verified?.Number > 120), "양쪽 확정 프레임");
        UnityEngine.Object.FindAnyObjectByType<QuantumGameView>().ReturnToMenu();
        await WaitUntil(() =>
        {
            visitor.Tick();
            return !guest.IsBusy && !flow.IsBusy;
        }, "게스트 로비 복귀");
        Require(SceneManager.GetActiveScene().name == "Main" && flow.Lobby == host && store.Joinable &&
            !host.IsReady && !visitor.IsReady && flow.Content == null &&
            sessions.All(session => session.Runner == null && session.Client == null && !session.HasInputSubscription) &&
            clients.All(client => !client.IsConnected) && QuantumGameSession.ActiveInputSubscriptions == 0 &&
            !QuantumRunner.ActiveRunners.Any(), "복귀 후 러너·연결·입력·맵 정리");
    }

    private static async Task WaitUntil(Func<bool> ready, string label)
    {
        var deadline = DateTime.UtcNow.AddSeconds(35);
        while (!ready())
        {
            Require(DateTime.UtcNow < deadline, "제한 시간: " + label);
            await Task.Delay(50);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }

    private sealed class LobbyStore
    {
        public string[] Members = { "guest", "host" };
        public string Owner = "host";
        public string State;
        public bool Joinable = true;
        public readonly Dictionary<string, string> Ready = new();
    }

    private sealed class LobbyEndpoint : IQuantumLobby
    {
        private readonly LobbyStore store;
        public LobbyEndpoint(LobbyStore store, string id) { this.store = store; LocalId = id; }
        public string LocalId { get; }
        public string OwnerId => store.Owner;
        public string[] Members => store.Members;
        public string Code => "test";
        public string MemberName(string member) => member;
        public string ReadState() => store.State;
        public bool WriteState(string value)
        {
            if (LocalId != OwnerId) return false;
            store.State = value;
            return true;
        }
        public string ReadReady(string member) => store.Ready.TryGetValue(member, out string ready) ? ready : null;
        public void WriteReady(string value) => store.Ready[LocalId] = value;
        public bool SetJoinable(bool value)
        {
            if (LocalId != OwnerId) return false;
            store.Joinable = value;
            return true;
        }
        public void Invite() { }
        public void Leave()
        {
            store.Members = Members.Where(member => member != LocalId).ToArray();
            store.Ready.Remove(LocalId);
            if (LocalId == OwnerId) store.Owner = Members.FirstOrDefault();
        }
    }

    private sealed class GuestGame : IQuantumLobbyGame
    {
        private readonly QuantumOfficialStageAsset stage;
        private readonly MapEditorPalette palette;
        public GuestGame(QuantumOfficialStageAsset stage, MapEditorPalette palette) { this.stage = stage; this.palette = palette; }
        public QuantumGameState State { get; private set; }
        public bool IsBusy => State is QuantumGameState.Starting or QuantumGameState.Playing or QuantumGameState.Stopping;
        public string Message { get; private set; }
        public QuantumGameSession Session { get; private set; }
        public bool Mismatch { get; set; }
        public string LastStartError { get; private set; }
        public bool CheckLobbyRelease() => false;
        public int LobbyMapMinimum(string map) => 1;
        public void ReportLobby(string message) => Message = message;
        public void RefreshLobby() { }
        public void DetachLobby(QuantumLobby lobby) { }
        public async Task StartLobbyAsync(QuantumLobbyState start, string localId, bool isOwner, MapSessionSnapshot source,
            bool isDevelopment, Action<string> regionReady)
        {
            State = QuantumGameState.Starting;
            LastStartError = null;
            var config = new RuntimeConfig { FlipFriendsPlayerCount = start.Members.Length };
            if (start.Map.StartsWith("official:", StringComparison.Ordinal)) QuantumGameContent.Prepare(stage, config);
            if (Mismatch) config.CustomMapAgreement += "-mismatch";
            var session = Session = new QuantumGameSession { IsInputEnabled = false };
            try
            {
                await session.StartLobbyAsync(config, null, 0, start, localId, isOwner, isDevelopment, palette, source,
                    null, regionReady, null);
                State = QuantumGameState.Playing;
            }
            catch (Exception error)
            {
                LastStartError = error.Message;
                if (State == QuantumGameState.Stopping) return;
                ReportLobby(error.Message);
                await session.StopAsync();
                Session = null;
                State = QuantumGameState.Failed;
            }
        }
        public async Task ReturnFromLobbyAsync(string message)
        {
            State = QuantumGameState.Stopping;
            if (Session != null) await Session.StopAsync();
            Session = null;
            State = QuantumGameState.Ready;
            ReportLobby(message);
        }
    }
}
