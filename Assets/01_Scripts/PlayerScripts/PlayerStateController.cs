using Mirror;
using UnityEngine;
using UnityEngine.Playables;

public class PlayerStateController : NetworkBehaviour
{
    public PlayerState playerState { get; private set; }

    public void ChangeState(PlayerState newState)
    {
        if (playerState == newState) return;

        playerState = newState;
    }
}

public enum PlayerState
{
    Idle,
    Walk,
    Jump,
    Damaged,
    Attack,
    Climb,
    Shrink,
    Carried,
    Throw
}