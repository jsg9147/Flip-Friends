using UnityEngine;
using UnityEngine.EventSystems;
using Mirror;

public class ButtonSelectHandler : MonoBehaviour, ISelectHandler
{
    public void OnSelect(BaseEventData eventData)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayClickSound();
    }
}
