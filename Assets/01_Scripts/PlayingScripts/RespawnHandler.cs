using Mirror;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    public LayerMask targetLayers; // ������ ���̾�
    public Transform resetPoint;  // ���� ����Ʈ ��ġ

    public bool onlyBoxReset;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer)
            return;

        // ���� ��ü�� ���̾ targetLayers�� ���ԵǾ� �ִ��� Ȯ��
        if (((1 << collision.gameObject.layer) & targetLayers) != 0)
        {
            // ���������� ��ġ�� �����ϵ��� ȣ��
            ResetTarget(collision.gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isServer)
            return;

        // ���� ��ü�� ���̾ targetLayers�� ���ԵǾ� �ִ��� Ȯ��
        if (((1 << collision.gameObject.layer) & targetLayers) != 0)
        {
            // ���������� ��ġ�� �����ϵ��� ȣ��
            ResetTarget(collision.gameObject);
        }
    }

    private void ResetTarget(GameObject target)
    {
        // 플레이어는 예측 이동을 하므로 위치를 직접 바꾸면 소유 클라이언트가 이전 예측 위치로 되돌린다. 서버 순간이동으로 처리한다.
        if (target.TryGetComponent(out ServerMover mover))
        {
            if (target.GetComponent<PlayerController2D>().isCarried) return;
            if (onlyBoxReset && !target.GetComponent<PlayerInteraction>().IsHoldingObject) return;
            mover.Teleport(resetPoint.position, Vector2.zero);
            return;
        }

        RpcPositionReset(target);
    }

    [ClientRpc] // ���������� ����
    private void RpcPositionReset(GameObject target)
    {
        if(target == null) return;
        if (onlyBoxReset)
        {
            PlayerInteraction targetPlayer = target.GetComponent<PlayerInteraction>();
            if(targetPlayer != null && !targetPlayer.IsHoldingObject)
            {
                return;
            }
        }
        target.transform.position = resetPoint.position;
    }
}
