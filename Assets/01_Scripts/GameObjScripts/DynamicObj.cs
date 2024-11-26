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
        SlimeRoomManager slimeRoomManager = (SlimeRoomManager)NetworkManager.singleton;

        if(slimeRoomManager != null)
        {
            int playerCount = slimeRoomManager.numPlayers - 1;
            transform.position = transform.position + new Vector3(xMoveValue * playerCount, yMoveValue * playerCount);
        }
    }
}
