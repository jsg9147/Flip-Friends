using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlacedObjectView : MonoBehaviour
{
    private PlacedObjectData data;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    public PlacedObjectData Data => data;
    public SpriteRenderer SpriteRenderer => spriteRenderer;

    public void Initialize(PlacedObjectData placedObjectData)
    {
        if (placedObjectData == null)
        {
            Debug.LogError($"배치 오브젝트 표현에 연결할 데이터가 없습니다: {name}", this);
            return;
        }

        data = placedObjectData;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
        ApplyDataToTransform();
    }

    public void SetSelected(bool isSelected, Color selectionColor)
    {
        if (spriteRenderer == null)
        {
            Debug.LogError($"선택 상태를 표시할 SpriteRenderer가 없습니다: {name}", this);
            return;
        }

        spriteRenderer.color = isSelected ? selectionColor : originalColor;
    }

    public void SetPosition(Vector3 position)
    {
        ApplyTransform(position, transform.eulerAngles.z, transform.localScale);
    }

    public void SetRotation(float rotation)
    {
        ApplyTransform(transform.position, rotation, transform.localScale);
    }

    public void SetScale(Vector3 scale)
    {
        ApplyTransform(transform.position, transform.eulerAngles.z, scale);
    }

    private void ApplyDataToTransform()
    {
        Vector3 position = data.position?.ToVector3() ?? Vector3.zero;
        Vector3 scale = data.scale?.ToVector3() ?? Vector3.one;
        ApplyTransform(position, data.rotation, scale);
    }

    private void ApplyTransform(Vector3 position, float rotation, Vector3 scale)
    {
        if (data == null)
        {
            Debug.LogError($"Transform을 반영할 배치 데이터가 없습니다: {name}", this);
            return;
        }

        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotation));
        transform.localScale = scale;
        data.position = new SerializableVector3(position);
        data.rotation = rotation;
        data.scale = new SerializableVector3(scale);
    }
}
