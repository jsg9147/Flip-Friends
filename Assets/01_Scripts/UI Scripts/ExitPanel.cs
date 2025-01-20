using UnityEngine;

public class ExitPanel : MonoBehaviour
{
    public void ExitGame()
    {
        GameManager.Instance.ExitGame();
    }
}
