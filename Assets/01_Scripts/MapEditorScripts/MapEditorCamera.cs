using UnityEngine;

public class MapEditorCamera : MonoBehaviour
{
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
        HandlePan();
        HandleZoom();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2))
        {
            panOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        if (Input.GetMouseButton(2))
        {
            // 클릭한 월드 좌표가 마우스 위치에 고정되도록 카메라 이동
            Vector3 delta = panOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
            cam.transform.position += delta;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize - scroll * zoomSpeed,
            minZoom, maxZoom
        );
    }
}
