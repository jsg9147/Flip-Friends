using System;
using FlipFriends.Maps;
using Quantum;
using UnityEngine;

namespace FlipFriends.Network
{
    public sealed class QuantumTestBootstrap : MonoBehaviour
    {
        [SerializeField] private RuntimeConfig runtimeConfig = new();
        [SerializeField] private bool isOnline;
        [SerializeField] private uint steamAppId;
        [SerializeField] private string roomName = "flip-friends-quantum-test";
        [SerializeField] private bool useScriptedInput;
        [SerializeField] private QuantumCustomMapAsset customMap;
        private QuantumGameSession session;
        private QuantumPlayerViews views;
        private bool isDestroyed;
        public int VerifiedFrame => session?.Runner?.Game.Frames.Verified?.Number ?? 0;
        public ulong VerifiedChecksum => session?.Runner?.Game.Frames.Verified?.CalculateChecksum() ?? 0;
        public RuntimeConfig Config => runtimeConfig;
        public void Configure(RuntimeConfig config) => runtimeConfig = config;
        public void ConfigureCustomMap(QuantumCustomMapAsset map) { customMap = map; ValidateCustomMap(); }

        public void ValidateCustomMap()
        {
            if (customMap != null) customMap.ValidateAndApply(runtimeConfig);
            else if (!string.IsNullOrEmpty(runtimeConfig.CustomMapAgreement))
                throw new InvalidOperationException("커스텀 맵 검증 에셋이 없습니다.");
        }

        private async void Start()
        {
            try
            {
                ValidateCustomMap();
                Application.runInBackground = true;
                session = new QuantumGameSession();
                if (useScriptedInput) session.InputSource = QuantumPlayerInput.ReadScripted;
                if (isOnline)
                    await session.StartOnlineAsync(runtimeConfig, new Photon.Realtime.AppSettings(PhotonServerSettings.Global.AppSettings), steamAppId, roomName);
                else await session.StartLocalAsync(runtimeConfig);
                if (!isDestroyed) views = new QuantumPlayerViews(transform);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Quantum 시험] 시작 실패: " + error.GetType().Name);
                if (session != null) await session.StopAsync();
            }
        }

        private void Update()
        {
            if (session?.Runner?.Game.Frames.Predicted != null) views?.Draw(session.Runner.Game.Frames.Predicted);
        }

        private async void OnDestroy()
        {
            isDestroyed = true;
            views?.Dispose();
            try { if (session != null) await session.StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
