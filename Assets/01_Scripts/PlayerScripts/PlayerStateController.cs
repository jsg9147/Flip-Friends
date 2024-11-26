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

        switch (newState)
        {
            case PlayerState.Idle:
                break;
            case PlayerState.Walk:
                break;
            case PlayerState.Jump:
                break;
            default:
                break;
        }
    }

    private void LayerChange()
    {

    }
}