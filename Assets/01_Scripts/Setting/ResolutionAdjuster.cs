using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ResolutionAdjuster : MonoBehaviour
{
    [SerializeField] private TMP_Text resolutionText; // 해상도를 표시할 Text
    [SerializeField] private GameObject targetUI; // 조정 후 돌아갈 첫 버튼

    private int currentIndex = 0; // 현재 선택된 해상도 인덱스
    private Resolution[] filteredResolutions; // 필터링된 해상도 목록

    // 목록이 짧고 순환해서, 누르고 있는 동안 반복하면 원하는 해상도를 지나치기 쉽다
    private readonly HorizontalStepInput stepInput = HorizontalStepInput.PressOnly();

    private const string ResolutionKey = "SavedResolution"; // PlayerPrefs 키
    private const int DefaultWidth = 1600; // 기본 해상도 너비
    private const int DefaultHeight = 900; // 기본 해상도 높이

    // Start가 아니라 Awake에서 목록을 만든다. 다른 스크립트가 Start에서 LoadResolution을 불러도 목록이 비어 있지 않게 하기 위함이다
    void Awake()
    {
        filteredResolutions = GetFilteredResolutions();

        LoadResolution();
        UpdateResolutionText();
    }

    void Update()
    {
        int direction = stepInput.Read(targetUI);

        if (direction != 0)
        {
            ChangeResolution(direction);
        }
    }

    private void ChangeResolution(int direction)
    {
        if (!HasResolutions())
        {
            return;
        }

        currentIndex += direction;

        // 인덱스 범위를 초과하지 않도록 순환
        if (currentIndex < 0)
            currentIndex = filteredResolutions.Length - 1;
        else if (currentIndex >= filteredResolutions.Length)
            currentIndex = 0;

        // 화면 텍스트 갱신. 실제 적용은 설정 화면을 벗어날 때 ApplyResolution이 맡는다
        UpdateResolutionText();
    }

    // 마우스 클릭도 키보드·게임패드 입력과 같은 해상도 전환 경로를 쓰도록 공개
    public void NextResolution()
    {
        ChangeResolution(1);
    }

    public void PreviousResolution()
    {
        ChangeResolution(-1);
    }

    private void UpdateResolutionText()
    {
        if (resolutionText == null || !HasResolutions())
        {
            return;
        }

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
        if (filteredResolutions == null)
        {
            filteredResolutions = GetFilteredResolutions();
        }

        if (TryGetSavedResolution(out int savedWidth, out int savedHeight))
        {
            int savedIndex = IndexOf(savedWidth, savedHeight);

            // 저장된 해상도가 현재 모니터 목록에 남아 있을 때만 그대로 쓴다
            if (savedIndex >= 0)
            {
                currentIndex = savedIndex;
                Screen.SetResolution(savedWidth, savedHeight, Screen.fullScreenMode);
                return;
            }
        }

        // 데이터가 없거나 유효하지 않은 경우 기본 해상도로 설정
        Screen.SetResolution(DefaultWidth, DefaultHeight, Screen.fullScreenMode);

        // 기본 해상도가 목록에 없으면 -1이 되어 표시·적용에서 터지므로 0으로 떨어뜨린다
        int defaultIndex = IndexOf(DefaultWidth, DefaultHeight);
        currentIndex = defaultIndex >= 0 ? defaultIndex : 0;
    }

    // 화면 모드도 여기서 같이 적용한다. 같은 프레임에 Screen.fullScreenMode를 따로 바꾸면 이전 해상도로 다시 요청되어 한쪽이 덮이기 때문이다
    public void ApplyResolution(FullScreenMode screenMode)
    {
        if (!HasResolutions())
        {
            Debug.LogWarning("사용할 수 있는 해상도 목록이 비어 있어 해상도를 적용하지 못했습니다.", this);
            return;
        }

        Resolution selectedResolution = filteredResolutions[currentIndex];

        // 설정 화면을 드나들 때마다 호출되므로 바뀐 게 없으면 아무것도 하지 않는다
        if (selectedResolution.width == Screen.width && selectedResolution.height == Screen.height
            && screenMode == Screen.fullScreenMode)
        {
            return;
        }

        Screen.SetResolution(selectedResolution.width, selectedResolution.height, screenMode);

        // 해상도를 PlayerPrefs에 저장
        PlayerPrefs.SetString(ResolutionKey, $"{selectedResolution.width}x{selectedResolution.height}");
        PlayerPrefs.Save();
    }

    private bool HasResolutions()
    {
        return filteredResolutions != null && filteredResolutions.Length > 0
            && currentIndex >= 0 && currentIndex < filteredResolutions.Length;
    }

    private bool TryGetSavedResolution(out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!PlayerPrefs.HasKey(ResolutionKey))
        {
            return false;
        }

        string[] resolutionParts = PlayerPrefs.GetString(ResolutionKey).Split('x');

        if (resolutionParts.Length != 2
            || !int.TryParse(resolutionParts[0], out width)
            || !int.TryParse(resolutionParts[1], out height))
        {
            Debug.LogWarning($"저장된 해상도 '{PlayerPrefs.GetString(ResolutionKey)}'를 읽을 수 없어 기본 해상도를 씁니다.", this);
            return false;
        }

        return true;
    }

    private int IndexOf(int width, int height)
    {
        return System.Array.FindIndex(filteredResolutions, res => res.width == width && res.height == height);
    }
}
