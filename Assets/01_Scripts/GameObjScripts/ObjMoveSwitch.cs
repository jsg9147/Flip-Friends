using UnityEngine;

public class ObjMoveSwitch : MonoBehaviour
{
    [SerializeField] private Vector2 moveDistance;
    [SerializeField] private SpriteRenderer switchSprite;
    [SerializeField] private Sprite offBtnSprite;
    [SerializeField] private Sprite onBtnSprite;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        
    }
}
