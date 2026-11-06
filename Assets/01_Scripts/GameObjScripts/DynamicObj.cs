using Mirror;
using UnityEngine;

public class DynamicObj : NetworkBehaviour
{
    public float xMoveValue;
    public float yMoveValue;
    private Vector3 initialPosition;
    private bool hasInitialPosition;

    private void CaptureInitialPosition()
    {
        if (hasInitialPosition) return;
        initialPosition = transform.position;
        hasInitialPosition = true;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (!(NetworkManager.singleton is SlimeRoomManager roomManager))
        {
            Debug.LogError($"{transform.root.name}/{name}: SlimeRoomManager를 찾을 수 없습니다.", this);
            return;
        }
        ServerApplyPlayerCount(roomManager.numPlayers);
    }

    [Server]
    public void ServerApplyPlayerCount(int playerCount)
    {
        if (!isServer) return;
        CaptureInitialPosition();
        int additionalPlayers = Mathf.Max(0, playerCount - 1);
        transform.position = initialPosition + new Vector3(xMoveValue * additionalPlayers, yMoveValue * additionalPlayers, 0);
    }
}
