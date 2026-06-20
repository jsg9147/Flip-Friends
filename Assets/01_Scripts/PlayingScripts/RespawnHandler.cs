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
            RpcPositionReset(collision.gameObject);
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
            RpcPositionReset(collision.gameObject);
        }
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
