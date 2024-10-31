using Mirror;
using UnityEngine;
using System.Collections;

public class PlayerSummonSwitch : NetworkBehaviour
{
    public Transform summonLocation;   // 플레이어들이 소환될 기준 위치
    public float cooldownTime = 5f;    // 스위치를 누른 후 쿨다운 시간 (초)

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
        SummonAllPlayers(); // 모든 플레이어 소환

        // 쿨다운 대기
        yield return new WaitForSeconds(cooldownTime);

        spriteRenderer.sprite = defaultSprite; // 스프라이트를 원래대로 복원
        isCooldown = false; // 쿨다운 종료
    }

    [Server]
    private void SummonAllPlayers()
    {
        int playerIndex = 0; // Y 좌표 증가를 위한 인덱스

        // 모든 플레이어의 위치를 summonLocation 기준으로 Y 좌표를 올려 이동시키는 RPC 호출
        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            NetworkIdentity playerIdentity = conn.identity;

            // 누른 플레이어가 아닌 경우만 소환
            if (playerIdentity != triggeringPlayer)
            {
                RpcSummonPlayer(playerIdentity, playerIndex);
                playerIndex++; // 다음 플레이어의 Y 좌표를 1씩 증가
            }
        }
    }

    [ClientRpc]
    private void RpcSummonPlayer(NetworkIdentity player, int playerIndex)
    {
        // 클라이언트에서 각 플레이어를 소환 위치로 이동
        if (player != null)
        {
            Vector3 newPosition = summonLocation.position;
            newPosition.y += playerIndex; // 인덱스에 따라 Y 좌표를 1씩 증가
            player.transform.position = newPosition;
        }
    }
}
