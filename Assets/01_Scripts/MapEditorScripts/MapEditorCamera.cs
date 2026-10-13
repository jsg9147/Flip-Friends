using UnityEngine;
using UnityEngine.InputSystem;

public class MapEditorCamera : MonoBehaviour
{
    private const float MinimumZoomDifference = 0.01f;
    // Input System은 휠 한 칸을 ±1로 주므로, 기존 Mouse ScrollWheel 축(한 칸 0.1)과 같은 줌 속도로 맞춘다.
    private const float ScrollToLegacyAxisScale = 0.1f;

    [Header("이동")]
    [SerializeField] private Vector2 minimumPosition = new(-50f, -30f);
    [SerializeField] private Vector2 maximumPosition = new(50f, 30f);

    [Header("줌")]
    [SerializeField] private float zoomSpeed = 3f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 25f;

    private Camera cam;
    private Vector3 panOrigin;

    private void Awake()
    {
        cam = Camera.main;
        if (cam != null) return;

        Debug.LogError("MapEditorCamera가 제어할 MainCamera를 찾지 못했습니다.", this);
        enabled = false;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        HandlePan(mouse);
        HandleZoom(mouse);
    }

    private void HandlePan(Mouse mouse)
    {
        Vector3 pointerPosition = mouse.position.ReadValue();
        if (mouse.middleButton.wasPressedThisFrame)
        {
            panOrigin = cam.ScreenToWorldPoint(pointerPosition);
        }

        if (mouse.middleButton.isPressed)
        {
            // 클릭한 월드 좌표가 마우스 위치에 고정되도록 카메라 이동
            Vector3 delta = panOrigin - cam.ScreenToWorldPoint(pointerPosition);
            cam.transform.position += delta;
            ClampPosition();
        }
    }

    private void HandleZoom(Mouse mouse)
    {
        float scroll = mouse.scroll.ReadValue().y * ScrollToLegacyAxisScale;
        if (Mathf.Abs(scroll) < 0.001f) return;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - scroll * zoomSpeed,
            minZoom, maxZoom
        );
    }

    private void ClampPosition()
    {
        Vector3 position = cam.transform.position;
        position.x = Mathf.Clamp(position.x, minimumPosition.x, maximumPosition.x);
        position.y = Mathf.Clamp(position.y, minimumPosition.y, maximumPosition.y);
        cam.transform.position = position;
    }

    private void OnValidate()
    {
        if (minimumPosition.x > maximumPosition.x)
            (minimumPosition.x, maximumPosition.x) = (maximumPosition.x, minimumPosition.x);
        if (minimumPosition.y > maximumPosition.y)
            (minimumPosition.y, maximumPosition.y) = (maximumPosition.y, minimumPosition.y);

        minZoom = Mathf.Max(MinimumZoomDifference, minZoom);
        maxZoom = Mathf.Max(minZoom + MinimumZoomDifference, maxZoom);
        zoomSpeed = Mathf.Max(MinimumZoomDifference, zoomSpeed);
    }
}
