using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;
using UnityEngine.EventSystems;
using System;

public class HostSetting : MonoBehaviour
{
    public Button roomTypeButton;
    public Button maxPlayerCountButton;
    public Button createButton;
    public Button cancelButton;

    private RoomType roomType = RoomType.Public;
    private int maxPlayerCount = 4;
    private int minPlayerLimit = 2;
    private int maxPlayerLimit = 4;

    private bool canNavigate = true; // 입력 제한 플래그
    private float inputCooldown = 0.2f; // 입력 간 최소 대기 시간
    private float lastInputTime; // 마지막 입력 시간 기록

    private void Awake()
    {
        // 초기 UI 설정
        UpdateButtonText();

        // 버튼 클릭 이벤트 등록
        createButton.onClick.AddListener(CreateRoom);
        cancelButton.onClick.AddListener(Cancel);
    }

    private void OnEnable()
    {
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent += Cancel;
    }

    private void OnDisable()
    {
        if (InputManager.instance != null)
            InputManager.instance.OnCancelEvent -= Cancel;
    }

    private void Update()
    {
        if (canNavigate)
        {
            if (InputManager.instance.dir.x != 0 && Time.time - lastInputTime > inputCooldown)
            {
                int dir = (int)InputManager.instance.dir.x;

                if (EventSystem.current.currentSelectedGameObject == roomTypeButton.gameObject)
                {
                    ToggleRoomType();
                }
                else if (EventSystem.current.currentSelectedGameObject == maxPlayerCountButton.gameObject)
                {
                    ChangeMaxPlayerCount(dir);
                }
                else if(EventSystem.current.currentSelectedGameObject == createButton.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
                }
                else if(EventSystem.current.currentSelectedGameObject == cancelButton.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(createButton.gameObject);
                }

                lastInputTime = Time.time; // 마지막 입력 시간 갱신
                canNavigate = false; // 입력 제한
            }
        }

        // 입력이 릴리즈되었는지 확인
        if (InputManager.instance.dir.x == 0)
        {
            canNavigate = true; // 입력 제한 해제
        }
    }

    private void UpdateButtonText()
    {
        // 각 버튼의 텍스트 업데이트
        roomTypeButton.GetComponentInChildren<TMP_Text>().text = $"{roomType}";
        maxPlayerCountButton.GetComponentInChildren<TMP_Text>().text = $"{maxPlayerCount}";
    }

    public void ToggleRoomType()
    {
        roomType = roomType == RoomType.Public ? RoomType.Private : RoomType.Public;
        UpdateButtonText();
    }

    public void ChangeMaxPlayerCount(int count)
    {
        maxPlayerCount += count;
        maxPlayerCount = Mathf.Clamp(maxPlayerCount, minPlayerLimit, maxPlayerLimit);

        UpdateButtonText();
    }

    private void CreateRoom()
    {
        Debug.Log($"Room Created: Type={roomType}, MaxPlayers={maxPlayerCount}");
        // 여기에 방 생성 로직 추가
        SteamRoomManager roomManager = NetworkManager.singleton as SteamRoomManager;
        if (roomManager != null)
        {
            roomManager.HostLobby(roomType, maxPlayerCount);
        }
    }

    private void Cancel()
    {
        MainUIManager.instance.GameModeUIOpen();
    }
}

public enum RoomType
{
    Private,
    Public
}
