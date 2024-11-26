using UnityEngine;
using Mirror;
using TMPro;

public class PlayerController2D : NetworkBehaviour
{
    public TMP_Text nameText;
    public GameObject readySprite;

    private PlayerAnimationController animationController;
    private PlayerInput inputHandler;
    private MovementHandler movementHandler;
    private PlayerStateController stateController;
    private PlayerInteraction interactionController;
    
    private SpriteRenderer spriteRenderer;
    private CameraController cameraController;

    private int ropeCollisionCount = 0; // Rope 충돌 상태를 추적

    [SyncVar(hook = nameof(OnFlipChanged))]
    private bool flipSprite; // SyncVar로 flipX 상태 동기화

    [SyncVar(hook = nameof(PlayerNameUpdate))] 
    public string playerName = "No Name";

    [SyncVar(hook = nameof(FinishCheck))] public bool isFinish;
    [SyncVar(hook = nameof(SetPlayerReady))] private bool isReady;

    [Command]
    public void CmdSetPlayerName(string playerName) => this.playerName = playerName;

    [Command]
    public void CmdSetPlayerReady(bool isReady) =>this.isReady = isReady;

    private void SetPlayerReady(bool oldValue, bool newValue)
    {
        readySprite.SetActive(newValue);
        Debug.Log($"{playerName}의 준비상태가 {oldValue} 에서 {newValue} 되었습니다");
    }

    private void PlayerNameUpdate(string oldName, string newName)
    {
        nameText.text = newName;
        Debug.Log($"플레이어 이름이 {oldName}에서 {newName}으로 변경되었습니다.");
    }

    private void Start()
    {
        InitializeComponents();
        SetupCamera();

        if (isOwned && SteamRoomManager.Instance != null)
        {
            CmdSetPlayerName(SteamRoomManager.Instance.playerName);
        }
    }

    private void Update()
    {
        if (!isOwned) return;

        HandleHorizontalMovement();
        HandleJump();
        ObjectInteraction();
        PositionReset();
    }

    private void FixedUpdate()
    {
        if (!isOwned) return;

        UpdatePlayerState();
    }

    private void InitializeComponents()
    {
        animationController = GetComponent<PlayerAnimationController>();
        inputHandler = GetComponent<PlayerInput>();
        movementHandler = GetComponent<MovementHandler>();
        stateController = GetComponent<PlayerStateController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionController = GetComponent<PlayerInteraction>();
    }

    private void SetupCamera()
    {
        if (isLocalPlayer && Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
            cameraController?.SetTarget(transform);
        }
    }

    private void HandleHorizontalMovement()
    {
        movementHandler.SetDirectionalInput(inputHandler.MovementInput, inputHandler.IsRunPressed);
    }

    private void HandleJump()
    {
        if (inputHandler.IsJumpPressed)
        {
            movementHandler.OnJumpInputDown();
            UpdatePlayerStateAndAnimation(PlayerState.Jump);
        }

        if (inputHandler.IsJumpUp)
        {
            movementHandler.OnJumpInputUp();
        }
    }

    private void PositionReset()
    {
        if (inputHandler.ResetPressed)
        {
            transform.position = Vector3.zero;
        }
    }

    private void UpdatePlayerState()
    {
        if (movementHandler.isGrounded)
        {
            if (Mathf.Abs(movementHandler.CurrentVelocity.x) > 0.05f)
            {
                UpdateFlipState(movementHandler.CurrentVelocity.x < 0);
                UpdatePlayerStateAndAnimation(PlayerState.Walk);
            }
            else
            {
                UpdatePlayerStateAndAnimation(PlayerState.Idle);
            }
        }

        animationController.GroundState(movementHandler.isGrounded);
    }

    private void UpdateFlipState(bool isFlip)
    {
        if (flipSprite != isFlip)
        {
            CmdFlipChanged(isFlip);
        }
    }

    private void UpdatePlayerStateAndAnimation(PlayerState newState)
    {
        stateController.ChangeState(newState);

        if (isOwned)
            animationController.CmdChangeAnimation(stateController.playerState);
    }

    [Command]
    private void CmdFlipChanged(bool isFlip)
    {
        flipSprite = isFlip;
    }

    private void OnFlipChanged(bool oldFlip, bool newFlip)
    {
        spriteRenderer.flipX = newFlip;
    }

    private void HandleDamage(Collider2D collision)
    {
        if (!collision.CompareTag("Trap") && !collision.CompareTag("Enemy")) return;

        Vector2 knockbackDirection = (transform.position - collision.transform.position).normalized;
        BasicTrap trap = collision.GetComponent<BasicTrap>();

        if (trap != null && trap.knockbackDir != Vector2.zero)
        {
            knockbackDirection = trap.knockbackDir;
        }

        UpdatePlayerStateAndAnimation(PlayerState.Damaged);
        movementHandler.OnDamaged(knockbackDirection);
    }

    private void HandleClimbing(Collider2D collision)
    {
        if (!collision.CompareTag("Rope")) return;

        // 위/아래 입력이 있는 경우만 등반 상태로 전환
        if (inputHandler.MovementInput.y != 0 && !interactionController.IsCarried)
        {
            movementHandler.SetClimbState(true);
            UpdatePlayerStateAndAnimation(PlayerState.Climb);
        }
    }

    private void ExitClimbing(Collider2D collision)
    {
        if (!collision.CompareTag("Rope")) return;

        // Rope와의 충돌이 종료되었으므로 감소
        ropeCollisionCount--;

        // 더 이상 Rope에 닿아있지 않으면 등반 상태 종료
        if (ropeCollisionCount <= 0)
        {
            ropeCollisionCount = 0; // 음수 방지
            movementHandler.SetClimbState(false);
            UpdatePlayerStateAndAnimation(PlayerState.Idle);
        }
    }
    [ClientRpc]
    public void RpcTargetFunction()
    {
        UpdatePlayerStateAndAnimation(PlayerState.Shrink);
    }

    private void ObjectInteraction()
    {
        if (inputHandler.IsPickUpPressed)
        {
            Vector3 dir = GetComponent<SpriteRenderer>().flipX ? Vector3.left : Vector3.right;
            interactionController.TryIntractive(dir);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleDamage(collision);
        HandleClimbing(collision);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Rope"))
        {
            ropeCollisionCount++; // Rope와의 충돌이 시작되면 증가
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        ExitClimbing(collision);
    }

    private void FinishCheck(bool oldValue, bool newValue)
    {
        PlayerSetActive(newValue);

        if (newValue)
        {
            GameManager.Instance.FinishCheck();
        }
    }
    private void PlayerSetActive(bool value)
    {
        GetComponent<SpriteRenderer>().enabled = !value;
        nameText.enabled = !value;
        GetComponent<BoxCollider2D>().enabled = !value;

        if (value)
        {
            //rb.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            //rb.bodyType = RigidbodyType2D.Dynamic;
            if (isLocalPlayer)
            {
                cameraController.SetTarget(transform);
                isFinish = false;
            }
        }
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
    ClimbIdle,
    Shrink,
    Carried,
    Throw
}