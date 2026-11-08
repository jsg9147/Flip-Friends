using System;
using FlipFriends.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlipFriends.Network
{
    public sealed class QuantumGameView : MonoBehaviour
    {
        [SerializeField] private Sprite playerSprite;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button returnButton;
        private QuantumPlayerViews players;
        private Sprite whiteSprite;
        private QuantumBoxViews boxes;
        private QuantumDeviceViews devices;
        private bool isReturning;
        private InputManager inputManager;

        private void Start()
        {
            var flow = QuantumGameFlow.Instance;
            if (flow?.Content == null) { ReturnToMenu(); return; }
            players = new QuantumPlayerViews(transform, playerSprite);
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one / 2, 1);
            boxes = new QuantumBoxViews(transform, whiteSprite);
            devices = new QuantumDeviceViews(transform, whiteSprite, status.font, flow.Content);
            foreach (var box in flow.Content.Boxes) AddBox(box, new Color(0.2f, 0.4f, 0.4f), "정적 지형 표시");
            foreach (var box in flow.Content.Ropes) AddBox(box, new Color(0.85f, 0.65f, 0.3f), "로프 표시");
            foreach (var box in flow.Content.Goals) AddBox(box, new Color(0.3f, 0.9f, 0.4f, 0.6f), "도착점 표시");
            if (flow.Content.TerrainContours != null)
            foreach (var contour in flow.Content.TerrainContours)
            for (int i = 0; i < contour.Vertices.Length; i++)
            {
                var a = contour.Vertices[i]; var b = contour.Vertices[(i + 1) % contour.Vertices.Length];
                var edge = new GameObject("지형 윤곽 " + contour.Id); edge.transform.SetParent(transform, false);
                edge.transform.position = new Vector3((a.X.AsFloat + b.X.AsFloat) / 2, (a.Y.AsFloat + b.Y.AsFloat) / 2);
                var delta = new Vector2(b.X.AsFloat - a.X.AsFloat, b.Y.AsFloat - a.Y.AsFloat);
                edge.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                edge.transform.localScale = new Vector3(delta.magnitude, 0.08f, 1);
                var renderer = edge.AddComponent<SpriteRenderer>(); renderer.sprite = whiteSprite; renderer.color = Color.white;
            }
            returnButton.onClick.AddListener(ReturnToMenu);
            inputManager = InputManager.instance;
            if (inputManager != null) inputManager.OnMenuEvent += ReturnToMenu;
            status.text = "1P A/D/W/S/Space/왼쪽 Shift/E | 2P 방향키/오른쪽 Ctrl/오른쪽 Shift/Enter\nR/Backspace: 리셋 | E/Enter: 들기/던지기 | 아래+들기: 놓기 | 1.5초 후 이동/점프: 탈출 | 도착점 위: 도착";
        }

        private void AddBox(QuantumMapBox box, Color color, string label)
        {
            var view = new GameObject(label);
            view.transform.SetParent(transform, false);
            view.transform.position = new Vector3(box.Center.X.AsFloat, box.Center.Y.AsFloat, 0);
            view.transform.localScale = new Vector3(box.Size.X.AsFloat, box.Size.Y.AsFloat, 1);
            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
        }

        private void Update()
        {
            var flow = QuantumGameFlow.Instance;
            var session = flow?.Session;
            if (isReturning || session?.Runner == null) return;
            if (!session.Runner.IsRunning)
            {
                Finish("Quantum 세션이 중단되었습니다. 준비 화면에서 다시 시작할 수 있습니다.");
                return;
            }
            var frame = session.Runner.Game.Frames;
            if (frame.Predicted != null) players?.Draw(frame.Predicted);
            if (frame.Predicted != null) boxes?.Draw(frame.Predicted);
            if (frame.Predicted != null) devices?.Draw(frame.Predicted);
            if (flow.Content.HasTerrain && frame.Predicted != null && Camera.main != null)
            {
                var query = frame.Predicted.Filter<Quantum.Transform2D, Quantum.FlipFriendsPlayer>();
                var min = new Vector2(float.MaxValue, float.MaxValue); var max = new Vector2(float.MinValue, float.MinValue); int count = 0;
                while (query.Next(out _, out var t, out var p)) if (!p.IsFinished && !p.RespawnPending)
                { var point = new Vector2(t.Position.X.AsFloat, t.Position.Y.AsFloat); min = Vector2.Min(min, point); max = Vector2.Max(max, point); count++; }
                if (count > 0)
                {
                    var center = (min + max) / 2; var camera = Camera.main;
                    camera.transform.position = new Vector3(center.x, center.y + 2, -20);
                    camera.orthographicSize = Mathf.Max(6, (max.x - min.x + 4) / (2 * camera.aspect), (max.y - min.y) / 2 + 4);
                }
            }
            if (frame.Verified != null && frame.Verified.FlipFriendsCompletedFrame > 0)
                Finish("전원 도착! 지원 맵을 완료했습니다.");
            else if (session.Client != null && (!session.Client.IsConnectedAndReady || session.Client.CurrentRoom?.PlayerCount != 2))
                Finish("참가자 연결이 종료되어 준비 화면으로 돌아왔습니다. 다시 시작할 수 있습니다.");
        }

        public void ReturnToMenu() => Finish("Quantum 플레이를 종료했습니다. 다시 시작할 수 있습니다.");

        private async void Finish(string message)
        {
            if (isReturning) return;
            isReturning = true;
            try
            {
                if (QuantumGameFlow.Instance != null) await QuantumGameFlow.Instance.ReturnAsync(message);
                else await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Main");
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void OnDestroy()
        {
            if (inputManager != null) inputManager.OnMenuEvent -= ReturnToMenu;
            if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToMenu);
            players?.Dispose();
            boxes?.Dispose();
            devices?.Dispose();
            if (whiteSprite != null) Destroy(whiteSprite);
        }
    }
}
