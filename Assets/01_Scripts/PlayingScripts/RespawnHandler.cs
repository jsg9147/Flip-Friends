using Mirror;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    public LayerMask targetLayers; // 감지할 레이어
    public Transform resetPoint;  // 리셋 포인트 위치

    public bool onlyBoxReset;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return;

        // 닿은 물체의 레이어가 targetLayers에 포함되어 있는지 확인
        if (((1 << collision.gameObject.layer) & targetLayers) != 0)
        {
            // 서버에서만 위치를 리셋하도록 호출
            RpcPositionReset(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isServer)
            return;

        // 닿은 물체의 레이어가 targetLayers에 포함되어 있는지 확인
        if (((1 << collision.gameObject.layer) & targetLayers) != 0)
        {
            // 서버에서만 위치를 리셋하도록 호출
            RpcPositionReset(collision.gameObject);
        }
    }

    [ClientRpc] // 서버에서만 실행
    private void RpcPositionReset(GameObject target)
    {
        if(target == null) return;
        if (onlyBoxReset)
        {
            PlayerInteraction targetPlayer = target.GetComponent<PlayerInteraction>();
            if(targetPlayer != null && !targetPlayer.IsCarried)
            {
                return;
            }
        }
        target.transform.position = resetPoint.position;
    }
}
