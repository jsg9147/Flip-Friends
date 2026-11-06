using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RespawnHandler : NetworkBehaviour
{
    public LayerMask targetLayers;
    public Transform resetPoint;
    public bool onlyBoxReset;

    private readonly HashSet<GameObject> resetTargets = new HashSet<GameObject>();
    private float lastContactTime = float.NegativeInfinity;

    private void OnTriggerEnter2D(Collider2D collision) => ResetTarget(collision.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => ResetTarget(collision.gameObject);

    [ServerCallback]
    private void ResetTarget(GameObject target)
    {
        if (!isServer) return;
        PickupObj box = target.GetComponentInParent<PickupObj>();
        // 기존 상자 자체 Reset 접촉은 Player 전용 마스크에서도 작동했다.
        bool isBoxReset = box != null && CompareTag("Reset");
        if (!isBoxReset && ((1 << target.layer) & targetLayers.value) == 0) return;
        if (resetPoint == null)
        {
            Debug.LogError($"{transform.root.name}/{name}: 필수 resetPoint가 없습니다.", this);
            return;
        }
        GameObject resetTarget = box != null ? box.gameObject : target;
        if (lastContactTime != Time.fixedTime)
        {
            resetTargets.Clear();
            lastContactTime = Time.fixedTime;
        }
        if (!resetTargets.Add(resetTarget)) return;
        if (box != null)
            box.ServerReset(resetPoint.position);
        else if (target.TryGetComponent(out PlayerController2D player))
            player.ServerResetSection(resetPoint.position, onlyBoxReset);
        else
            Debug.LogError($"{transform.root.name}/{name}: 지원하지 않는 리셋 대상 {target.name}", target);
    }
}
