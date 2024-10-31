using Mirror;
using UnityEngine;

public class DynamicObj : NetworkBehaviour
{
    public float xMoveValue;
    public float yMoveValue;

    public override void OnStartServer()
    {
        base.OnStartServer();
        UpdateDynamicObjPos();
    }


    private void UpdateDynamicObjPos()
    {
        int playerCount = MirrorRoomManager.Instance.numPlayers - 1;
        transform.position = transform.position + new Vector3(xMoveValue * playerCount, yMoveValue * playerCount);
    }
}
