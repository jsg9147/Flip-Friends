using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource uiSource;

    [Header("BGM Clips")]
    public AudioClip[] bgmClips; // BGM 파일들을 배열로 관리

    [Header("UI Sounds")]
    public AudioClip clickSound;

    private const string BgmVolumeKey = "BGMVolume";
    private const string SfxVolumeKey = "SFXVolume";
    private const float DefaultVolume = 1.0f;

    private void Awake()
    {
        // 싱글톤 패턴 구현
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴되지 않도록 설정
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 설정 화면이 없는 씬에서 시작해도 저장된 음량을 따른다. 값 저장은 ValueAdjuster가 맡는다
        ApplyBGMVolume(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultVolume));
        ApplySFXVolume(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume));
    }

    /// <summary>
    /// BGM 재생
    /// </summary>
    /// <param name="clipIndex">재생할 BGM의 인덱스</param>
    public void PlayBGM(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= bgmClips.Length)
        {
            Debug.LogError("잘못된 BGM 인덱스입니다.");
            return;
        }

        if (bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }

        bgmSource.clip = bgmClips[clipIndex];
        bgmSource.Play();
    }

    /// <summary>
    /// 현재 BGM 일시정지
    /// </summary>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary>
    /// 일시정지된 BGM 재개
    /// </summary>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.Play();
        }
    }

    /// <summary>
    /// BGM 정지
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PlayClickSound()
    {
        uiSource.clip = clickSound;
        uiSource.Play();
    }

    /// <summary>
    /// BGM 볼륨 적용
    /// </summary>
    /// <param name="volume">적용할 볼륨 (0.0f ~ 1.0f)</param>
    public void ApplyBGMVolume(float volume)
    {
        bgmSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// 효과음 볼륨 적용
    /// </summary>
    /// <param name="volume">적용할 볼륨 (0.0f ~ 1.0f)</param>
    public void ApplySFXVolume(float volume)
    {
        // UI 클릭음도 효과음 설정을 따른다
        uiSource.volume = Mathf.Clamp01(volume);
    }
}
