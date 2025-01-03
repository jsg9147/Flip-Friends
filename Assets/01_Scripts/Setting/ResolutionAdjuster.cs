using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class ResolutionAdjuster : MonoBehaviour
{
    public TMP_Text resolutionText; // 해상도를 표시할 Text
    public GameObject targetUI; // 조정 후 돌아갈 첫 버튼

    private int currentIndex = 0; // 현재 선택된 해상도 인덱스
    private Resolution[] filteredResolutions; // 필터링된 해상도 목록

    private const string ResolutionKey = "SavedResolution"; // PlayerPrefs 키
    private const int DefaultWidth = 1600; // 기본 해상도 너비
    private const int DefaultHeight = 900; // 기본 해상도 높이

    void Start()
    {
        // 필터링된 해상도를 가져옴
        filteredResolutions = GetFilteredResolutions();

        // PlayerPrefs에서 저장된 해상도 불러오기
        LoadResolution();

        // 초기 해상도를 설정
        UpdateResolutionText();
    }

    void Update()
    {
        if (EventSystem.current.currentSelectedGameObject == targetUI)
        {
            HandleAdjustmentInput();
        }
    }

    private void HandleAdjustmentInput()
    {
        if (InputManager.instance.dir.x > 0)
        {
            ChangeResolution(1); // 오른쪽 입력으로 다음 해상도
        }
        else if (InputManager.instance.dir.x < 0)
        {
            ChangeResolution(-1); // 왼쪽 입력으로 이전 해상도
        }
    }

    private void ChangeResolution(int direction)
    {
        currentIndex += direction;

        // 인덱스 범위를 초과하지 않도록 클램프
        if (currentIndex < 0)
            currentIndex = filteredResolutions.Length - 1;
        else if (currentIndex >= filteredResolutions.Length)
            currentIndex = 0;

        // 화면 텍스트 갱신
        UpdateResolutionText();
    }

    private void UpdateResolutionText()
    {
        Resolution res = filteredResolutions[currentIndex];
        resolutionText.text = $"{res.width} x {res.height}";
    }

    private Resolution[] GetFilteredResolutions()
    {
        // 해상도를 중복 없이 저장할 HashSet 생성
        HashSet<(int width, int height)> uniqueResolutions = new HashSet<(int, int)>();
        List<Resolution> filteredList = new List<Resolution>();

        foreach (var res in Screen.resolutions)
        {
            // 이미 등록된 해상도가 아니면 추가
            if (uniqueResolutions.Add((res.width, res.height)))
            {
                filteredList.Add(res);
            }
        }

        // 결과를 배열로 변환
        return filteredList.ToArray();
    }


    public void LoadResolution()
    {
        // 저장된 해상도 불러오기
        if (PlayerPrefs.HasKey(ResolutionKey))
        {
            string savedResolution = PlayerPrefs.GetString(ResolutionKey);
            string[] resolutionParts = savedResolution.Split('x');
            int savedWidth = int.Parse(resolutionParts[0]);
            int savedHeight = int.Parse(resolutionParts[1]);

            // 저장된 해상도가 필터링된 해상도 목록에 있는지 확인
            for (int i = 0; i < filteredResolutions.Length; i++)
            {
                if (filteredResolutions[i].width == savedWidth &&
                    filteredResolutions[i].height == savedHeight)
                {
                    currentIndex = i;
                    Screen.SetResolution(savedWidth, savedHeight, Screen.fullScreen);
                    return;
                }
            }
        }

        // 데이터가 없거나 유효하지 않은 경우 기본 해상도로 설정
        Screen.SetResolution(DefaultWidth, DefaultHeight, Screen.fullScreen);
        if (filteredResolutions != null)
        {
            currentIndex = System.Array.FindIndex(filteredResolutions, res =>
            res.width == DefaultWidth && res.height == DefaultHeight);
        }
    }

    public void ApplyResolution()
    {
        Resolution selectedResolution = filteredResolutions[currentIndex];
        Screen.SetResolution(selectedResolution.width, selectedResolution.height, Screen.fullScreen);

        // 해상도를 PlayerPrefs에 저장
        PlayerPrefs.SetString(ResolutionKey, $"{selectedResolution.width}x{selectedResolution.height}");
        PlayerPrefs.Save();
    }
}
