
using UnityEngine;

public class SavePoint : MonoBehaviour
{
    public int savePointID; // 세이브 포인트 고유 ID

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            var player = collision.GetComponent<PlayerController2D>();
            if (player != null)
            {
                player.SetSavePoint(this);
            }
        }
    }
}
