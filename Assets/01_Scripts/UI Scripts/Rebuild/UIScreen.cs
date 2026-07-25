using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIScreen : MonoBehaviour
{
    [SerializeField] private string screenId;
    [SerializeField] private GameObject firstSelectedObject;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool hideOnAwake = true;

    public string ScreenId => screenId;
    public bool IsVisible { get; private set; }

    protected virtual void Reset()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    protected virtual void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (hideOnAwake)
        {
            HideImmediate();
        }
    }

    public virtual void Show()
    {
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        IsVisible = true;
        OnShow();
        SelectDefault();
    }

    public virtual void Hide()
    {
        if (!gameObject.activeSelf && !IsVisible)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        IsVisible = false;
        OnHide();
        LogDeactivate(nameof(Hide));
        gameObject.SetActive(false);
    }

    public virtual void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        IsVisible = false;
        LogDeactivate(nameof(HideImmediate));
        gameObject.SetActive(false);
    }

    public virtual void SelectDefault()
    {
        if (firstSelectedObject == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(firstSelectedObject);
    }

    protected virtual void OnShow()
    {
    }

    protected virtual void OnHide()
    {
    }

    private void LogDeactivate(string sourceMethod)
    {
        string resolvedScreenId = string.IsNullOrWhiteSpace(screenId) ? "<empty>" : screenId;
        Debug.Log($"[UIScreen] Deactivate '{name}' (ScreenId: {resolvedScreenId}) via {sourceMethod}.", this);
    }
}
