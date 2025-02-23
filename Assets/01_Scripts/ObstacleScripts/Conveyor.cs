using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class Conveyor : NetworkBehaviour
{
    [SerializeField] private List<Animator> beltAnimators;

    public bool isClockwise { private set; get; }

    private readonly Color activeColor = new Color(0.35f, 0.43f, 0.88f, 1f);
    private readonly Color inactiveColor = new Color(0.86f, 0.29f, 0.3f, 1f);

    private void Start()
    {
        foreach (var animator in beltAnimators)
        {
            SetAnimatorState("isMoving", true);
            SetAnimatorState("isClockwise", true);
            isClockwise = true;
        }
    }

    public void UpdateConveyorState(bool isActivated)
    {
        isClockwise = isActivated;
        SetAnimatorState("isClockwise", isActivated);
        UpdateBeltColor();
    }

    private void SetAnimatorState(string stateName, bool state)
    {
        foreach (var animator in beltAnimators)
        {
            animator.SetBool(stateName, state);
        }
    }

    private void UpdateBeltColor()
    {
        foreach (var animator in beltAnimators)
        {
            if (animator.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                spriteRenderer.color = isClockwise ? activeColor : inactiveColor;
            }
        }
    }
}
