using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도트 프레임 버튼 공통 프리팹의 라벨·아이콘 배치를 맡는다.
// 라벨을 프레임 테두리 안쪽으로 늘려 두어, 버튼 크기를 바꿔도 글자가 테두리와 겹치지 않는다.
[RequireComponent(typeof(Button))]
public sealed class PixelButton : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;
    [SerializeField] private Vector2 frameInset = new(24, 14);
    [SerializeField] private float iconGap = 8;
    [Header("선택 상태")]
    [SerializeField] private Sprite highlightFrame;
    [SerializeField] private Color highlightLabelColor = new Color32(7, 53, 56, 255);

    private Button button;
    private Sprite normalFrame;
    private Color normalLabelColor;
    private bool hasNormalStyle;

    public Button Button => button != null ? button : button = GetComponent<Button>();
    public TMP_Text Label => label;

    public string Text
    {
        get => label.text;
        set => label.text = value;
    }

    public void SetIcon(Sprite sprite, float size)
    {
        bool hasIcon = sprite != null;
        icon.sprite = sprite;
        icon.gameObject.SetActive(hasIcon);
        icon.rectTransform.sizeDelta = new Vector2(size, size);
        icon.rectTransform.anchoredPosition = new Vector2(frameInset.x + size * 0.5f, 0);
        SetLabelInsets(hasIcon ? frameInset.x + size + iconGap : frameInset.x, frameInset.x);
    }

    public void SetLabelInsets(float left, float right)
    {
        label.rectTransform.offsetMin = new Vector2(left, frameInset.y);
        label.rectTransform.offsetMax = new Vector2(-right, -frameInset.y);
    }

    public void SetFontRange(float min, float max)
    {
        label.fontSizeMin = min;
        label.fontSizeMax = max;
    }

    // 맵 에디터의 현재 도구·팔레트처럼 고른 항목을 연두 프레임으로 구분한다.
    public void SetHighlighted(bool isHighlighted)
    {
        if (!hasNormalStyle)
        {
            normalFrame = Button.image.sprite;
            normalLabelColor = label.color;
            hasNormalStyle = true;
        }
        Button.image.sprite = isHighlighted && highlightFrame != null ? highlightFrame : normalFrame;
        label.color = isHighlighted ? highlightLabelColor : normalLabelColor;
    }
}
