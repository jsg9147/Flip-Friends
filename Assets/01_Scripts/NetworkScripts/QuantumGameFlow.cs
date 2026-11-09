using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlipFriends.Maps;
using Photon.Realtime;
using Quantum;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace FlipFriends.Network
{
    public enum QuantumGameState { Ready, Starting, Playing, Stopping, Failed }

    public sealed class QuantumGameFlow : MonoBehaviour
    {
        public const string PlayScene = "QuantumPlay";
        [SerializeField] private QuantumCustomMapAsset[] supportedMaps;
        [SerializeField] private uint steamAppId;
        [SerializeField] private string releasePhotonAppId;
        public static QuantumGameFlow Instance { get; private set; }
        public QuantumGameState State { get; private set; }
        public string Message { get; private set; } = "맵과 참가 인원을 선택하고 장치 배정을 확인하세요.";
        public QuantumCustomMapAsset[] SupportedMaps => supportedMaps;
        // 공식 스테이지는 기존 사용례 뒤의 선택 번호를 쓴다. 기존 사용례 번호(0~5)는 바꾸지 않는다.
        public IReadOnlyList<QuantumOfficialStageAsset> OfficialStages { get; private set; } = Array.Empty<QuantumOfficialStageAsset>();
        public int MapCount => supportedMaps.Length + OfficialStages.Count;
        public int OfficialMapIndex(int stage) => supportedMaps.Length + stage;
        public IQuantumMapContent SelectedContent => SelectedMap < supportedMaps.Length ? supportedMaps[SelectedMap] : OfficialStages[SelectedMap - supportedMaps.Length];
        public int SelectedMap { get; private set; }
        public int PlayerCount { get; private set; } = 2;
        public int KeyboardSlots { get; private set; } = 2;
        public bool HasLocalDevices => (KeyboardSlots == 0 || Keyboard.current != null) &&
            Gamepad.all.Count >= PlayerCount - KeyboardSlots;
        public QuantumGameSession Session { get; private set; }
        public QuantumMapConversion Content { get; private set; }
        public event Action Changed;
        private bool isDestroyed;
        private bool isSceneTransition;
        private Task operation;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            var interactionMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsInteractionMapManifest");
            if (interactionMap != null && !supportedMaps.Contains(interactionMap)) supportedMaps = supportedMaps.Concat(new[] { interactionMap }).ToArray();
            var respawnMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsRespawnMapManifest");
            if (respawnMap != null && !supportedMaps.Contains(respawnMap)) supportedMaps = supportedMaps.Concat(new[] { respawnMap }).ToArray();
            var deviceMap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsDeviceMapManifest");
            if (deviceMap != null && !supportedMaps.Contains(deviceMap)) supportedMaps = supportedMaps.Concat(new[] { deviceMap }).ToArray();
            var tilemap = Resources.Load<QuantumCustomMapAsset>("FlipFriendsTilemapMapManifest");
            if (tilemap != null && !supportedMaps.Contains(tilemap)) supportedMaps = supportedMaps.Concat(new[] { tilemap }).ToArray();
            try { OfficialStages = QuantumOfficialStageCatalog.LoadStages(); }
            catch (InvalidOperationException error) { Debug.LogException(error, this); }
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.runInBackground = true;
        }

        public void SelectMap(int index)
        {
            if (State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping) return;
            if (index < 0 || index >= MapCount) return;
            SelectedMap = index;
            SetState(QuantumGameState.Ready, "선택: " + SelectedContent.DisplayName);
        }

        public void CycleParticipants()
        {
            if (State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping) return;
            if (KeyboardSlots > 0) KeyboardSlots--;
            else
            {
                PlayerCount = PlayerCount % QuantumGameContent.MaxPlayers + 1;
                KeyboardSlots = Math.Min(2, PlayerCount);
            }
            Changed?.Invoke();
        }

        public Task StartAsync(bool isOnline)
        {
            if (State == QuantumGameState.Starting || State == QuantumGameState.Playing || State == QuantumGameState.Stopping)
                return operation ?? Task.CompletedTask;
            SetState(QuantumGameState.Starting, isOnline ? "Steam 인증 및 Photon 연결 중… 취소할 수 있습니다." : "콘텐츠 검증 및 Quantum 시작 중…");
            return operation = StartCoreAsync(isOnline);
        }

        private async Task StartCoreAsync(bool isOnline)
        {
            try
            {
                var config = new RuntimeConfig { FlipFriendsPlayerCount = PlayerCount };
                Content = QuantumGameContent.Prepare(SelectedContent, config);
                if (!isOnline && !HasLocalDevices) throw new InvalidOperationException("배정할 키보드 또는 게임패드를 연결하세요.");
                Session = new QuantumGameSession { KeyboardCount = KeyboardSlots, IsInputEnabled = false };
                if (isOnline)
                {
                    var settings = new AppSettings(PhotonServerSettings.Global.AppSettings) { AppIdQuantum = releasePhotonAppId };
                    await Session.StartOnlineAsync(config, settings, steamAppId, "ff-release-" + config.CustomMapAgreement);
                }
                else await Session.StartLocalAsync(config);
                if (isDestroyed || State != QuantumGameState.Starting) return;
                isSceneTransition = true;
                await SceneManager.LoadSceneAsync(PlayScene);
                isSceneTransition = false;
                if (isDestroyed || State != QuantumGameState.Starting) return;
                Session.IsInputEnabled = true;
                SetState(QuantumGameState.Playing, "도착점에서 위를 입력하세요. 메뉴로 종료할 수 있습니다.");
            }
            catch (Exception error)
            {
                if (Session != null) await Session.StopAsync();
                Session = null;
                Content = null;
                if (State != QuantumGameState.Stopping && !isDestroyed)
                {
                    string reason = error is InvalidOperationException ? error.Message : "연결 또는 시작에 실패했습니다. 인증 설정과 연결 상태를 확인하고 재시도하세요.";
                    Debug.LogWarning("[Quantum] 시작 실패: " + error.GetType().Name);
                    SetState(QuantumGameState.Failed, reason);
                }
            }
            finally { isSceneTransition = false; }
        }

        public async Task ReturnAsync(string message)
        {
            if (State == QuantumGameState.Stopping) { if (operation != null) await operation; return; }
            SetState(QuantumGameState.Stopping, "세션 정리 중…");
            var previous = operation;
            var session = Session;
            if (session != null) await session.StopAsync();
            if (previous != null) await previous;
            Session = null;
            Content = null;
            if (isDestroyed) return;
            if (SceneManager.GetActiveScene().name != "Main")
            {
                isSceneTransition = true;
                await SceneManager.LoadSceneAsync("Main");
                isSceneTransition = false;
            }
            SetState(QuantumGameState.Ready, message);
        }

        private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (isSceneTransition || (State != QuantumGameState.Playing && State != QuantumGameState.Starting) || scene.name == PlayScene) return;
            try { await ReturnAsync("씬 이동으로 Quantum 세션을 종료했습니다."); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void SetState(QuantumGameState state, string message)
        {
            State = state;
            Message = message;
            Changed?.Invoke();
        }

        private async void OnDestroy()
        {
            if (Instance != this) return;
            isDestroyed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
            Changed = null;
            try { if (Session != null) await Session.StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
