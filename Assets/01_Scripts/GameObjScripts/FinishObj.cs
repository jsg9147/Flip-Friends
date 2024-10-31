using UnityEngine;
using Mirror;

public class FinishObj : NetworkBehaviour
{
    // 나중에 특정 키를 눌러야 클리어 되고 다시 다른 키를 누르면 클리어 취소 할수 있게
    // 혹시 모를 상황
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && isServer)
        {
            Finish(collision.GetComponent<PlayerController>());
        }
    }

    private void Finish(PlayerController player)
    {
        player.CmdFinishState();
    }
}
