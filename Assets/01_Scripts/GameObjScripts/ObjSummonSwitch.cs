using Mirror;
using UnityEngine;
using System.Collections;

public class ObjSummonSwitch : NetworkBehaviour
{
    public Transform summonLocation;   // 플레이어들이 소환될 기준 위치
    public float cooldownTime = 5f;    // 스위치를 누른 후 쿨다운 시간 (초)
    public Transform targetObjTrans;

    private bool isCooldown = false;   // 쿨다운 상태 확인
    private NetworkIdentity triggeringPlayer; // 스위치를 누른 플레이어

    public Sprite defaultSprite;       // 기본 스프라이트
    public Sprite pressedSprite;       // 눌렀을 때의 스프라이트

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if (isServer && !isCooldown)
            {
                triggeringPlayer = collision.GetComponent<NetworkIdentity>(); // 누른 플레이어 저장
                StartCoroutine(SummonAllPlayersWithCooldown());
            }
        }
    }

    [Server]
    private IEnumerator SummonAllPlayersWithCooldown()
    {
        isCooldown = true;
        spriteRenderer.sprite = pressedSprite; // 스프라이트를 눌린 상태로 변경
        SummonTargetObject(); 

        // 쿨다운 대기
        yield return new WaitForSeconds(cooldownTime);

        spriteRenderer.sprite = defaultSprite; // 스프라이트를 원래대로 복원
        isCooldown = false; // 쿨다운 종료
    }

    [Server]
    private void SummonTargetObject()
    {
        targetObjTrans.position = summonLocation.position;
        RpcSummonTargetObject();
    }

    [ClientRpc]
    private void RpcSummonTargetObject()
    {
        targetObjTrans.position = summonLocation.position;
    }
}
