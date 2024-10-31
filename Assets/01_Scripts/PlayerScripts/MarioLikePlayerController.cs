using UnityEngine;
using Mirror;
using System.Collections;

public class MarioLikePlayerController : NetworkBehaviour
{
    public float moveSpeed = 4f;
    public float runMultiplier = 1.5f;
    public float jumpForce = 20f;
    public float minJumpForce = 10f; // Minimum jump force
    public float maxRunJumpMultiplier = 15f; // Maximum jump force multiplier when running
    public float jumpHoldDuration = 0.2f;
    public float throwForce = 15f;
    public float movementForceMultiplier = 2f; // Configurable parameter for movement force
    public float pickupRadius = 1f; // Configurable radius for pickup detection
    public float dampingFactor = 0.005f; // Configurable damping factor for reducing sliding
    public LayerMask interactableLayerMask; // Layer mask for interactable objects
    private bool isRunning = false;

    private Rigidbody2D rb;
    private bool isJumping = false;
    private float jumpTime;
    private GameObject heldObject = null;
    [SyncVar] private bool isPickingOrThrowing = false;
    [SyncVar] private bool canPickUp = true;
    [SyncVar] private bool isDropping = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Smooth out physics updates
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleMovement();
        HandleJump();
        HandleThrowOrDrop();
        HandleEscape();
    }

    void HandleMovement()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");
        isRunning = Input.GetKey(KeyCode.LeftShift);

        float currentSpeed = isRunning ? moveSpeed * runMultiplier : moveSpeed;
        float maxSpeed = currentSpeed;

        if (Mathf.Abs(moveInput) > 0.01f)
        {
            float appliedForce = moveInput * currentSpeed * movementForceMultiplier;
            rb.AddForce(new Vector2(appliedForce, 0f));
        }

        // Apply damping to reduce sliding effect when input is released
        if (Mathf.Abs(moveInput) < 0.01f && Mathf.Abs(rb.linearVelocity.x) > 0.1f)
        {
            rb.AddForce(new Vector2(-rb.linearVelocity.x * dampingFactor, 0f), ForceMode2D.Impulse);
        }

        Vector2 clampedVelocity = rb.linearVelocity;
        clampedVelocity.x = Mathf.Clamp(clampedVelocity.x, -maxSpeed, maxSpeed);
        rb.linearVelocity = clampedVelocity;
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && Mathf.Abs(rb.linearVelocity.y) < 0.001f)
        {
            isJumping = true;
            jumpTime = 0f;
            float runJumpMultiplier = (!isJumping && isRunning) ? maxRunJumpMultiplier : 1f;
            rb.AddForce(new Vector2(0f, minJumpForce * runJumpMultiplier), ForceMode2D.Impulse);
        }

        if (Input.GetButton("Jump") && isJumping)
        {
            if (jumpTime < jumpHoldDuration)
            {
                rb.AddForce(Vector2.up * jumpForce * Time.deltaTime, ForceMode2D.Impulse);
                jumpTime += Time.deltaTime;
            }
        }

        if (Input.GetButtonUp("Jump"))
        {
            isJumping = false;
        }
    }

    void HandleThrowOrDrop()
    {
        if (isPickingOrThrowing || !canPickUp) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject != null)
            {
                isPickingOrThrowing = true;
                CmdThrowObject(heldObject, transform.right * throwForce);
                heldObject = null;
                StartCoroutine(PreventImmediatePickup());
            }
            else
            {
                Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, pickupRadius, interactableLayerMask);
                foreach (Collider2D collider in colliders)
                {
                    if (collider.gameObject != gameObject)
                    {
                        MarioLikePlayerController otherPlayerController = collider.GetComponent<MarioLikePlayerController>();
                        if (otherPlayerController != null && otherPlayerController.heldObject == null && collider.transform.parent == null)
                        {
                            isPickingOrThrowing = true;
                            heldObject = collider.gameObject;
                            CmdPickUpObject(heldObject, gameObject);
                            StartCoroutine(AllowEscapeAfterDelay(heldObject));
                            break;
                        }
                    }
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Q) && heldObject != null && !isDropping)
        {
            isDropping = true;
            CmdDropObject(heldObject, gameObject);
            heldObject = null;
        }
    }

    void HandleEscape()
    {
        if (heldObject != null && Input.GetKeyDown(KeyCode.Space) && !isDropping)
        {
            isDropping = true;
            CmdDropObject(heldObject, gameObject);
            heldObject = null;
        }
    }

    IEnumerator AllowEscapeAfterDelay(GameObject objectHeld)
    {
        yield return new WaitForSeconds(1f);
        if (objectHeld != null && objectHeld.transform.parent == transform)
        {
            // Allow the held player to escape by pressing the space key
            objectHeld.GetComponent<MarioLikePlayerController>().enabled = true;
        }
    }

    IEnumerator PreventImmediatePickup()
    {
        canPickUp = false;
        yield return new WaitForSeconds(1f);
        canPickUp = true;
    }

    [Command]
    void CmdPickUpObject(GameObject objectToPickUp, GameObject picker)
    {
        if (!CanExecuteCommand()) return;
        isPickingOrThrowing = true;
        RpcPickUpObject(objectToPickUp, picker);
        StartCoroutine(ResetPickOrThrowStateAfterCommand());
    }

    [ClientRpc]
    void RpcPickUpObject(GameObject objectToPickUp, GameObject picker)
    {
        if (objectToPickUp != null && picker != null)
        {
            objectToPickUp.transform.SetParent(picker.transform);
            objectToPickUp.transform.localPosition = new Vector3(1f, 0f, 0f);
            Rigidbody2D objectRb = objectToPickUp.GetComponent<Rigidbody2D>();
            if (objectRb != null)
            {
                objectRb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }

    [Command]
    void CmdThrowObject(GameObject objectToThrow, Vector2 force)
    {
        if (!CanExecuteCommand() || objectToThrow == null) return;
        isPickingOrThrowing = true;
        RpcThrowObject(objectToThrow, force);
        StartCoroutine(ResetPickOrThrowStateAfterCommand());
    }

    [ClientRpc]
    void RpcThrowObject(GameObject objectToThrow, Vector2 force)
    {
        if (objectToThrow != null)
        {
            objectToThrow.transform.SetParent(null);
            Rigidbody2D objectRb = objectToThrow.GetComponent<Rigidbody2D>();
            if (objectRb != null)
            {
                objectRb.bodyType = RigidbodyType2D.Dynamic;
                objectRb.linearVelocity = force;
            }
        }
    }

    [Command]
    void CmdDropObject(GameObject objectToDrop, GameObject dropper)
    {
        if (!CanExecuteCommand() || objectToDrop == null || dropper == null) return;
        isDropping = true;
        RpcDropObject(objectToDrop, dropper);
        StartCoroutine(ResetPickOrThrowStateAfterCommand());
    }

    [ClientRpc]
    void RpcDropObject(GameObject objectToDrop, GameObject dropper)
    {
        if (objectToDrop != null && dropper != null)
        {
            objectToDrop.transform.SetParent(null);
            objectToDrop.transform.position = dropper.transform.position + new Vector3(1f, 0f, 0f);
            Rigidbody2D objectRb = objectToDrop.GetComponent<Rigidbody2D>();
            if (objectRb != null)
            {
                objectRb.bodyType = RigidbodyType2D.Dynamic;
            }
        }
    }

    IEnumerator ResetPickOrThrowStateAfterCommand()
    {
        yield return new WaitForSeconds(1f); // Ensure state reset happens after command execution is completed
        isPickingOrThrowing = false;
        isDropping = false;
    }

    [Server]
    private bool CanExecuteCommand()
    {
        return !isPickingOrThrowing && !isDropping;
    }
}
