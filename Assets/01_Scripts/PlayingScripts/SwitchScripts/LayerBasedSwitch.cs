using Mirror;
using UnityEngine;

public class LayerBasedSwitch : Switch
{
    [SerializeField]
    private LayerMask activationLayer; // 활성화할 레이어 설정

    public virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLayerMatched(other.gameObject))
        {
            ToggleSwitch();
        }
    }

    public virtual void OnTriggerExit2D(Collider2D other)
    {
        // 접촉이 끊어졌을 때도 레이어가 맞으면 스위치 비활성화
        if (IsLayerMatched(other.gameObject))
        {
            ToggleSwitch();
        }
    }

    // 레이어가 설정된 값과 일치하는지 확인하는 메서드
    private bool IsLayerMatched(GameObject obj)
    {
        return (activationLayer.value & (1 << obj.layer)) > 0;
    }

    // 스위치 상태가 변경되었을 때의 동작 정의
    protected override void OnSwitchStateChanged(bool newState)
    {
        if (!isServer)
            return;

        RpcOnSwitchSpriteChange(newState);
    }

    [ClientRpc]
    private void RpcOnSwitchSpriteChange(bool newState)
    {
        GetComponent<SpriteRenderer>().sprite = newState ? onSwitchSprite : offSwitchSprite;
    }
}
