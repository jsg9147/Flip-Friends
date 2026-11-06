using UnityEngine;
using System.Collections.Generic;
using Mirror;

public class Conveyor : NetworkBehaviour
{
    [SerializeField] private List<Animator> beltAnimators = new List<Animator>();
    [SyncVar(hook = nameof(OnDirectionChanged))] private bool isMovingClockwise = true;

    private static readonly int MovingParameter = Animator.StringToHash("isMoving");
    private static readonly int DirectionParameter = Animator.StringToHash("isClockwise");
    private readonly Color activeColor = new Color(0.35f, 0.43f, 0.88f, 1f);
    private readonly Color inactiveColor = new Color(0.86f, 0.29f, 0.3f, 1f);

    public bool isClockwise => isMovingClockwise;

    public override void OnStartServer()
    {
        base.OnStartServer();
        ApplyVisualState();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyVisualState();
    }

    [Server]
    public void UpdateConveyorState(bool isActivated)
    {
        if (!isServer) return;
        isMovingClockwise = isActivated;
        ApplyVisualState();
    }

    private void OnDirectionChanged(bool oldState, bool newState) => ApplyVisualState();

    private void ApplyVisualState()
    {
        foreach (Animator animator in beltAnimators)
        {
            if (animator == null) continue;
            animator.SetBool(MovingParameter, true);
            animator.SetBool(DirectionParameter, isMovingClockwise);
            if (animator.TryGetComponent(out SpriteRenderer spriteRenderer))
                spriteRenderer.color = isMovingClockwise ? activeColor : inactiveColor;
        }
    }
}
