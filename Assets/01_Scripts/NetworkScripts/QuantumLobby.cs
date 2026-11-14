using System;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using UnityEngine;

#if UNITY_EDITOR
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif

namespace FlipFriends.Network
{
    internal interface IQuantumLobby
    {
        string LocalId { get; }
        string OwnerId { get; }
        string[] Members { get; }
        string Code { get; }
        string MemberName(string member);
        string ReadState();
        bool WriteState(string value);
        string ReadReady(string member);
        void WriteReady(string value);
        bool SetJoinable(bool value);
        void Leave();
        void Invite();
    }

    internal interface IQuantumLobbyGame
    {
        QuantumGameState State { get; }
        bool IsBusy { get; }
        string Message { get; }
        bool CheckLobbyRelease();
        int LobbyMapMinimum(string map);
        void ReportLobby(string message);
        void RefreshLobby();
        void DetachLobby(QuantumLobby lobby);
        Task StartLobbyAsync(QuantumLobbyState start, string localId, bool isOwner, MapSessionSnapshot source,
            bool isDevelopment, Action<string> regionReady);
        Task ReturnFromLobbyAsync(string message);
    }

    [Serializable]
    internal sealed class QuantumLobbyState
    {
        public string Owner;
        public string Epoch;
        public string Map;
        public string Name;
        public string Attempt;
        public string Region;
        public string[] Members;
        public string Notice;

        public bool HasAttempt => !string.IsNullOrEmpty(Attempt);
        public string Room => "ff-lobby-" + Attempt;

        public static QuantumLobbyState Read(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var value = JsonUtility.FromJson<QuantumLobbyState>(json);
                if (value?.Members == null || value.Members.Length == 0 || value.Members.Length > QuantumGameContent.MaxPlayers ||
                    !Guid.TryParseExact(value.Epoch, "N", out _) ||
                    (value.HasAttempt && (!Guid.TryParseExact(value.Attempt, "N", out _) || string.IsNullOrEmpty(value.Map))))
                    return null;
                return value;
            }
            catch (ArgumentException) { return null; }
        }
    }

    internal sealed class QuantumLobby
    {
        private const double StartTimeout = 75;
        private readonly IQuantumLobby lobby;
        private readonly IQuantumLobbyGame flow;
        private readonly Func<double> clock;
        private readonly bool isDevelopment;
        private QuantumLobbyState state;
        private QuantumLobbyState active;
        private MapSessionSnapshot source;
        private string selectedMap;
        private string selectedName;
        private double deadline;
        private bool hasStarted;
        private bool isLeaving;
        private bool isReturning;
        private string lastUiState;
        private string retiredAttempt;
        private bool needsReopen;

        public QuantumLobby(IQuantumLobby lobby, IQuantumLobbyGame flow, bool isDevelopment = false, Func<double> clock = null)
        {
            this.lobby = lobby;
            this.flow = flow;
            this.isDevelopment = isDevelopment;
            this.clock = clock ?? (() => Time.realtimeSinceStartupAsDouble);
        }

        public bool IsOwner => lobby.OwnerId == lobby.LocalId;
        public bool IsLocked => state == null || state.HasAttempt || active != null || isReturning || flow.IsBusy;
        public bool IsReady => state != null && lobby.ReadReady(lobby.LocalId) == state.Epoch;
        public string Code => lobby.Code;
        public string MapName => state?.Name;
        public string Status => state == null ? "로비 정보 대기 중…" :
            string.Join("\n", state.Members.Select(id =>
                $"{(id == state.Owner ? "[방장] " : "")}{LobbyDisplayName(id)}   {(lobby.ReadReady(id) == state.Epoch ? "준비 완료" : "준비 전")}")
                .Concat(Enumerable.Repeat("빈 자리", QuantumGameContent.MaxPlayers - state.Members.Length)));
        public bool CanStart => IsOwner && StartBlockReason == null;

        public string StartBlockReason
        {
            get
            {
                if (IsLocked) return "연결 상태를 확인하는 중…";
                if (!state.Members.SequenceEqual(lobby.Members)) return "참가자 목록을 갱신하는 중…";
                if (string.IsNullOrEmpty(state.Map)) return "맵을 선택하세요.";
                int minimum = flow.LobbyMapMinimum(state.Map);
                if (minimum == 0) return "다른 맵을 선택하세요.";
                if (state.Members.Length < minimum) return $"이 맵은 최소 {minimum}명이 필요해요.";
                int waiting = state.Members.Count(id => lobby.ReadReady(id) != state.Epoch);
                return waiting == 0 ? null : $"{waiting}명이 아직 준비하지 않았어요.";
            }
        }

        private string LobbyDisplayName(string id)
        {
            string name = (lobby.MemberName(id) ?? id).Replace('\r', ' ').Replace('\n', ' ');
            var characters = new System.Globalization.StringInfo(name);
            return characters.LengthInTextElements > 24 ? characters.SubstringByTextElements(0, 24) + "…" : name;
        }

        private bool HasEveryoneReady(QuantumLobbyState value) =>
            value.Members.Length >= 1 && value.Members.Length <= QuantumGameContent.MaxPlayers &&
            value.Members.All(id => lobby.ReadReady(id) == value.Epoch);

        public void Invite() { if (!IsLocked) lobby.Invite(); }

        public void Select(string map, string name, MapSessionSnapshot snapshot = null)
        {
            if (!IsOwner || IsLocked) return;
            selectedMap = map;
            selectedName = name;
            source = snapshot;
            PublishIdle(lobby.Members, null);
        }

        public void ToggleReady()
        {
            if (IsLocked || string.IsNullOrEmpty(state.Map)) return;
            lobby.WriteReady(IsReady ? "-" : state.Epoch);
            Notify();
        }

        public void Start()
        {
            if (IsLocked) return;
            if (!CanStart || string.IsNullOrEmpty(state.Map) || !state.Members.SequenceEqual(lobby.Members))
            {
                flow.ReportLobby("확정된 맵과 전원 준비가 필요합니다.");
                return;
            }
            if (!isDevelopment && !flow.CheckLobbyRelease()) return;
            int minimum = flow.LobbyMapMinimum(state.Map);
            if (minimum == 0 || state.Members.Length < minimum ||
                (state.Map.StartsWith("saved:", StringComparison.Ordinal) && source?.ContentHash != state.Map.Substring(6)))
            {
                flow.ReportLobby("선택한 맵과 최소 참가 인원을 확인하세요.");
                return;
            }
            if (!lobby.SetJoinable(false))
            {
                flow.ReportLobby("Steam 로비 입장을 닫을 수 없습니다.");
                return;
            }
            var start = new QuantumLobbyState
            {
                Owner = state.Owner, Epoch = state.Epoch, Map = state.Map, Name = state.Name,
                Members = state.Members, Attempt = Guid.NewGuid().ToString("N")
            };
            if (lobby.WriteState(JsonUtility.ToJson(start)))
            {
                state = start;
                Begin(start);
            }
            else
            {
                ReopenLobby();
                flow.ReportLobby("Steam에 시작 정보를 게시할 수 없습니다.");
            }
        }

        public void Tick()
        {
            if (isLeaving) return;
            string owner = lobby.OwnerId;
            var members = lobby.Members;
            if (string.IsNullOrEmpty(owner) || !members.Contains(lobby.LocalId))
            {
                Leave("Steam 로비가 사라졌거나 연결이 끊겼습니다.");
                return;
            }
            var published = QuantumLobbyState.Read(lobby.ReadState());
            if (published?.Members == null || published.Owner != owner || string.IsNullOrEmpty(published.Epoch))
                published = null;

            if (active != null && !isReturning)
            {
                if (published?.Attempt != active.Attempt || !active.Members.SequenceEqual(members))
                    _ = ReturnAsync(published?.Notice ?? "방장 또는 참가자가 바뀌어 세션을 종료했습니다.");
                else if (!HasEveryoneReady(active))
                    _ = ReturnAsync("참가자가 준비 또는 세션을 종료했습니다.");
                else if (flow.State != QuantumGameState.Playing && clock() >= deadline)
                    _ = ReturnAsync("로비 시작 시간이 초과됐습니다.");
                else if (!hasStarted && !string.IsNullOrEmpty(published.Region))
                    Begin(published);
            }
            else if (active == null && !isReturning)
            {
                if (published?.HasAttempt == true && published.Attempt != retiredAttempt && published.Members.SequenceEqual(members) &&
                    HasEveryoneReady(published))
                    Begin(published);
                else if (IsOwner && (published == null || !published.Members.SequenceEqual(members) || published.HasAttempt))
                    PublishIdle(members, "로비 참가 상태가 바뀌었습니다. 다시 준비하세요.");
                else if (IsOwner && needsReopen)
                    ReopenLobby();
            }

            var latest = QuantumLobbyState.Read(lobby.ReadState());
            state = latest?.Members != null && latest.Owner == owner ? latest : null;
            string uiState = state == null ? null : $"{state.Epoch}:{state.Attempt}:{Status}";
            if (lastUiState != uiState)
            {
                lastUiState = uiState;
                Notify();
            }
        }

        private void Begin(QuantumLobbyState start)
        {
            if (active == null)
            {
                active = start;
                deadline = clock() + StartTimeout;
                flow.ReportLobby("Steam 참가자를 확정했습니다. Quantum 연결 대기 중…");
            }
            if (hasStarted || (!IsOwner && string.IsNullOrEmpty(start.Region))) return;
            hasStarted = true;
            _ = RunAsync(start);
        }

        private async Task RunAsync(QuantumLobbyState start)
        {
            await flow.StartLobbyAsync(start, lobby.LocalId, IsOwner, source, isDevelopment, region =>
            {
                start.Region = region;
                if (!lobby.WriteState(JsonUtility.ToJson(start)))
                    throw new InvalidOperationException("Steam에 Photon 지역을 게시할 수 없습니다.");
            });
            if (active == start || active?.Attempt == start.Attempt)
            {
                if (!isReturning && flow.State != QuantumGameState.Playing)
                    await ReturnAsync(flow.Message);
            }
        }

        public void Leave(string message)
        {
            if (isLeaving) return;
            Close();
            _ = ReturnAsync(message);
        }

        public void Close()
        {
            if (isLeaving) return;
            isLeaving = true;
            lobby.Leave();
        }

        public async Task ReturnAsync(string message)
        {
            if (isReturning) return;
            isReturning = true;
            if (!isLeaving) lobby.WriteReady("-");
            try
            {
                await flow.ReturnFromLobbyAsync(message);
            }
            finally
            {
                retiredAttempt = active?.Attempt ?? retiredAttempt;
                active = null;
                hasStarted = false;
                isReturning = false;
                if (isLeaving) flow.DetachLobby(this);
                else if (IsOwner) PublishIdle(lobby.Members, flow.Message);
                Notify();
            }
        }

        private QuantumLobbyState NewState(string[] members, string epoch, string notice) => new()
        {
            Owner = lobby.LocalId, Epoch = epoch, Map = selectedMap, Name = selectedName,
            Members = members, Notice = notice
        };

        private void PublishIdle(string[] members, string notice)
        {
            var idle = NewState(members, Guid.NewGuid().ToString("N"), notice);
            if (!lobby.WriteState(JsonUtility.ToJson(idle))) return;
            state = idle;
            ReopenLobby();
            Notify();
        }

        private void ReopenLobby() => needsReopen = !lobby.SetJoinable(true);

        private void Notify() => flow.RefreshLobby();
    }
}
