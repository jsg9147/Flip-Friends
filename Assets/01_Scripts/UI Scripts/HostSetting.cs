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

    private bool canNavigate = true; // �Է� ���� �÷���
    private float inputCooldown = 0.2f; // �Է� �� �ּ� ��� �ð�
    private float lastInputTime; // ������ �Է� �ð� ���

    private void Awake()
    {
        // �ʱ� UI ����
        UpdateButtonText();

        // ��ư Ŭ�� �̺�Ʈ ���
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

                lastInputTime = Time.time; // ������ �Է� �ð� ����
                canNavigate = false; // �Է� ����
            }
        }

        // �Է��� ������Ǿ����� Ȯ��
        if (InputManager.instance.dir.x == 0)
        {
            canNavigate = true; // �Է� ���� ����
        }
    }

    private void UpdateButtonText()
    {
        // �� ��ư�� �ؽ�Ʈ ������Ʈ
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
        SteamRoomManager roomManager = NetworkManager.singleton as SteamRoomManager;
        if (roomManager != null)
        {
            createButton.interactable = false;
            roomManager.HostLobby(roomType, maxPlayerCount);
        }
    }

    private void Cancel()
    {
        createButton.interactable = true;
        MainUIManager.instance.GameModeUIOpen();
    }
}

public enum RoomType
{
    Private,
    Public
}
