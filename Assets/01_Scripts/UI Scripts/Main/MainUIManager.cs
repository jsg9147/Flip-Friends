using UnityEngine;

// 화면 전환은 ScreenNavigator가 맡고, 이 컴포넌트는 Main 씬 진입 시 한 번 필요한 전역 설정만 적용한다.
public class MainUIManager : MonoBehaviour
{
    [SerializeField] private bool mouseLock;
    [SerializeField] private int targetFrameRate = 60;

    private void Start()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(0);
        }

        if (mouseLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Application.targetFrameRate = targetFrameRate;
    }
}
