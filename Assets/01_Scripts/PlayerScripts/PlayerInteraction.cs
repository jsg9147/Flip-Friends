using Mirror;
using UnityEngine;

public class PlayerInteraction : NetworkBehaviour
{
    public float throwForce = 3f;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    private GameObject heldObject;

    private Vector3 heldPos;

    public bool IsPickUpState => heldObject != null;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        heldPos = Vector3.up;
    }

    private void Update()
    {
        FollowToPlayer();
    }

    public void TryIntractive(Vector2 dir)
    {
        if(heldObject == null)
        {
            GameObject obj = SearchObject(dir);
            if (obj != null)
            {
                CmdPickUpObj(obj);
            }
        }
        else
        {
            CmdThrowObj(dir);
        }
    }

    [Command]
    private void CmdPickUpObj(GameObject pickableObj)
    {
        if (pickableObj.GetComponent<PickupObj>() != null)
        {
            if (!pickableObj.GetComponent<PickupObj>().IsCarried)
            {
                PickUpObj(pickableObj);
                RpcPickUpObj(pickableObj);
            }
        }
    }
    [ClientRpc]
    private void RpcPickUpObj(GameObject pickableObj)
    {
        if (pickableObj.GetComponent<PickupObj>() != null)
        {
            PickUpObj(pickableObj);
        }
    }

    private void PickUpObj(GameObject pickableObj)
    {
        if (pickableObj.GetComponent<PickupObj>() != null)
        {
            heldObject = pickableObj;
            pickableObj.GetComponent<PickupObj>().SetPickupState(transform, true);
            DisableCollisionWithHeldObject(pickableObj);
        }
    }

    [Command]
    private void CmdThrowObj(Vector2 dir)
    {
        ThrowObject(dir);
        RpcThrowObj(dir);
    }

    [ClientRpc]
    private void RpcThrowObj(Vector2 dir)
    {
        ThrowObject(dir);
    }

    void ThrowObject(Vector2 dir)
    {
        if (heldObject != null)
        {
            Rigidbody2D objectRb = heldObject.GetComponent<Rigidbody2D>();
            if (objectRb != null)
            {
                Vector2 force = new Vector2(dir.x, 1f) * throwForce;
                objectRb.linearVelocity = (force + rb.linearVelocity);
            }

            heldObject.GetComponent<PickupObj>()?.StateReset();

            EnableCollisionWithHeldObject(heldObject);

            heldObject = null;
        }
    }

    private void FollowToPlayer()
    {
        if (heldObject != null)
        {
            heldObject.transform.position = transform.position + heldPos;
        }
    }

    private GameObject SearchObject(Vector2 dir)
    {
        Vector2 boxSize = boxCollider.size;
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        float raySpacing = boxSize.y / 4f; // Use the height of the box to calculate spacing
        int rayCount = 5; // Total of 5 Raycasts
        float xPos = (dir.x > 0) ? boxCollider.bounds.max.x : boxCollider.bounds.min.x;

        for (int i = 0; i < rayCount; i++)
        {
            // Set the starting position of the Ray from bottom to top at fixed intervals
            Vector2 rayOrigin = new Vector2(xPos, boxCollider.bounds.min.y + (i * raySpacing));

            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, dir, 0.15f, LayerMask.GetMask("Pickable"));
            Debug.DrawRay(rayOrigin, dir * 0.15f, Color.red, 0.1f);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    GameObject obj = hit.collider.gameObject;
                    return obj;
                }
            }
        }
        return null;
    }

    void DisableCollisionWithHeldObject(GameObject objectToPickUp)
    {
        Collider2D heldCollider = objectToPickUp.GetComponent<Collider2D>();
        if (heldCollider != null && boxCollider != null)
        {
            Physics2D.IgnoreCollision(boxCollider, heldCollider, true);
        }
    }

    void EnableCollisionWithHeldObject(GameObject objectToRelease)
    {
        Collider2D heldCollider = objectToRelease.GetComponent<Collider2D>();
        if (heldCollider != null && boxCollider != null)
        {
            Physics2D.IgnoreCollision(boxCollider, heldCollider, false);
        }
    }
}
