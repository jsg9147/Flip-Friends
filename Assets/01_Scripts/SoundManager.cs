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
        bgmSource.volume = PlayerPrefs.GetFloat("BGMVolume", 1.0f);
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
    /// BGM 볼륨 설정
    /// </summary>
    /// <param name="volume">설정할 볼륨 (0.0f ~ 1.0f)</param>
    public void SetBGMVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.0f, 1.0f); // 볼륨 범위 제한
        bgmSource.volume = volume;
        PlayerPrefs.SetFloat("BGMVolume", volume); // 볼륨 값 저장
        PlayerPrefs.Save();
    }

    /// <summary>
    /// BGM 볼륨 설정
    /// </summary>
    /// <param name="volume">설정할 볼륨 (0.0f ~ 1.0f)</param>
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp(volume, 0.0f, 1.0f); // 볼륨 범위 제한
        //bgmSource.volume = volume;
        PlayerPrefs.SetFloat("SFXVolume", volume); // 볼륨 값 저장
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장된 볼륨 불러오기
    /// </summary>
    /// <returns>현재 저장된 볼륨 값</returns>
    public float GetBGMVolume()
    {
        return PlayerPrefs.GetFloat("BGMVolume", 1.0f);
    }
}
