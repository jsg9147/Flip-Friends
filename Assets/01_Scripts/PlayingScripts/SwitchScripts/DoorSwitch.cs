using UnityEngine;

public class DoorSwitch : LayerBasedSwitch
{
    public GameObject door;

    private GameObject cachedDoor;
    private SpriteRenderer doorRenderer;
    private Collider2D doorCollider;

    protected override void ApplySwitchVisual(bool newState)
    {
        base.ApplySwitchVisual(newState);
        if (cachedDoor != door)
        {
            cachedDoor = door;
            doorRenderer = door != null ? door.GetComponent<SpriteRenderer>() : null;
            doorCollider = door != null ? door.GetComponent<Collider2D>() : null;
        }
        if (doorRenderer == null || doorCollider == null)
        {
            Debug.LogError($"{transform.root.name}/{name}: 문 대상의 렌더러/콜라이더 참조가 잘못되었습니다.", this);
            return;
        }
        doorRenderer.enabled = !newState;
        doorCollider.enabled = !newState;
    }
}
