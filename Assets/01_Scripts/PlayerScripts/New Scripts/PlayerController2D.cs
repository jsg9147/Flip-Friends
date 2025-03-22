using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using TMPro;

public class PlayerController2D : NetworkBehaviour
{
    public TMP_Text nameText;
    public GameObject readySprite;

    private PlayerAnimationController animationController;
    private PlayerInputManager inputHandler;
    private MovementHandler movementHandler;
    private PlayerStateController stateController;
    private PlayerInteraction interactionController;
    private SpriteRenderer spriteRenderer;
    private CameraController cameraController;
    private PlayerSound soundController;

    public bool isCarried { get; private set; } 

    private SavePoint savePoint;

    [SyncVar]private int ropeCollisionCount;

    [SyncVar(hook = nameof(PlayerNameUpdate))] public string playerName = "No Name";
    [SyncVar(hook = nameof(FinishCheck))] public bool isFinish;
    [SyncVar(hook = nameof(SetPlayerReady))] private bool isReady;
    [SyncVar(hook = nameof(PlayerColorUpdate))] private Vector4 colorVec;

    [Command]
    public void CmdSetPlayerName(string name) => playerName = name;

    [Command]
    public void CmdSetPlayerColor(Vector4 playerColor) => colorVec = playerColor;

    [Command]
    public void CmdSetPlayerReady(bool ready) => isReady = ready;
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

    private void PlayerColorUpdate(Vector4 oldValue, Vector4 newValue)
    {
        GetComponent<SpriteRenderer>().color = newValue;
    }

    private void Start()
    {
        InitializeComponents();
        SetupCamera();

        if (isOwned && SteamRoomManager.Instance != null)
        {
            CmdSetPlayerName(SteamRoomManager.Instance.playerName);
        }
        if (isOwned)
        {
            CmdSetPlayerColor(new Vector4(PlayerPrefs.GetFloat("Red", 0.3f), PlayerPrefs.GetFloat("Green", 1.0f), PlayerPrefs.GetFloat("Blue", 1.0f), 1f));
        }
    }

    private void Update()
    {
        if (isOwned)
        {
            HandleInput();
        }
    }

    private void FixedUpdate()
    {
        if (isServer && !isFinish)
        {
            UpdatePlayerState();
        }
    }

    private void InitializeComponents()
    {
        animationController = GetComponent<PlayerAnimationController>();
        inputHandler = GetComponent<PlayerInputManager>();
        movementHandler = GetComponent<MovementHandler>();
        stateController = GetComponent<PlayerStateController>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionController = GetComponent<PlayerInteraction>();
        soundController = GetComponent<PlayerSound>();
    }

    private void SetupCamera()
    {
        if (isLocalPlayer && Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
            cameraController?.SetTarget(transform);
        }
    }

    private void HandleInput()
    {
        if (inputHandler.IsNextPressed)
        {
            cameraController.MoveNextTarget(1);
        }
        if (inputHandler.IsPreviousPressed)
        {
            cameraController.MoveNextTarget(-1);
        }

        if (isFinish)
        {
            HandleFinishState();
        }
        else
        {
            HandleMovement();
            HandleJump();

            if(inputHandler.IsPickUpPressed)
                CmdObjectInteraction();

            if (inputHandler.ResetPressed)
            {
                CmdPositionReset();
            }
        }
    }

    private void HandleFinishState()
    {

        if (inputHandler.MovementInput.y < 0)
        {
            SetFinishState(inputHandler.MovementInput);
        }
    }

    private void HandleMovement()
    {
        CmdHandleHorizontalMovement(inputHandler.MovementInput, inputHandler.IsRunPressed);
    }

    private void HandleJump()
    {
        if (inputHandler.IsJumpPressed)
        {
            CmdJumpInputDown();
        }

        CmdJumpInputHold(inputHandler.IsJumpHold);

        if (inputHandler.IsJumpUp)
        {
            CmdJumpInputUp();
        }
    }
    public void OnSteppedByOtherPlayer()
    {
        UpdatePlayerStateAndAnimation(PlayerState.Shrink);
        movementHandler.BlockJump(0.15f);
    }


    [Command]
    private void CmdHandleHorizontalMovement(Vector2 movementInput, bool isRun)
    {
        if (interactionController.IsCarried)
        {
            movementHandler.SetClimbState(false);
        }

        movementHandler.SetDirectionalInput(movementInput, isRun);
    }

    [Command]
    private void CmdJumpInputDown()
    {
        movementHandler.OnJumpInputDown();
        UpdatePlayerStateAndAnimation(PlayerState.Jump);
    }

    [Command]
    private void CmdJumpInputHold(bool isHold)
    {
        movementHandler.JumpHold(isHold);
    }

    [Command]
    private void CmdJumpInputUp()
    {
        movementHandler.OnJumpInputUp();
    }

    public void SetSavePoint(SavePoint nextPoint)
    {
        if (savePoint == null)
            savePoint = nextPoint;

        if(nextPoint.savePointID > savePoint.savePointID)
            savePoint = nextPoint;
    }

    [Command]
    private void CmdPositionReset()
    {
        Vector2 resetPos = Vector2.zero;
        if(savePoint != null)
            resetPos = savePoint.transform.position;
        transform.position = resetPos;
        movementHandler.RpcVelocityReset();
    }

    private void UpdatePlayerState()
    {
        if (movementHandler == null) return;

        if (movementHandler.isGrounded)
        {
            UpdateGroundedState();
        }

        animationController.RpcGroundState(movementHandler.isGrounded);
    }

    private void UpdateGroundedState()
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

    private void UpdateFlipState(bool isFlip)
    {
        if (spriteRenderer.flipX != isFlip)
        {
            RpcFlipChanged(isFlip);
        }
    }

    [ClientRpc]
    private void RpcFlipChanged(bool isFlip)
    {
        spriteRenderer.flipX = isFlip;
    }

    private void UpdatePlayerStateAndAnimation(PlayerState newState)
    {
        stateController.ChangeState(newState);

        if (isServer)
        {
            animationController.RpcChangeAnimation(stateController.playerState);
            if (newState == PlayerState.Shrink)
                GetComponent<PlayerSound>().RpcPlayShrinkSound();
        }
    }

    private void OnFlipChanged(bool oldFlip, bool newFlip)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = newFlip;
        }
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
        interactionController.TryIntractive(GetInteractionDirection(), true);
        movementHandler.OnDamaged(knockbackDirection);
    }

    private Vector3 GetInteractionDirection()
    {
        return spriteRenderer.flipX ? Vector3.left : Vector3.right;
    }

    [Command]
    private void CmdHandleClimbing()
    {
        if (interactionController.IsCarried) return;

        movementHandler.SetClimbState(true);

        if (movementHandler.isClimbed)
        {
            UpdatePlayerStateAndAnimation(PlayerState.Climb);
        }
    }

    [Command]
    private void CmdEnterRope()
    {
        ropeCollisionCount++;
    }
    [Command]
    private void CmdExitClimbing()
    {
        ropeCollisionCount = Mathf.Max(0, ropeCollisionCount - 1);

        if (ropeCollisionCount == 0)
        {
            movementHandler.SetClimbState(false);
            UpdatePlayerStateAndAnimation(PlayerState.Idle);
        }
    }

    [Command]
    private void CmdObjectInteraction()
    {
        interactionController.TryIntractive(GetInteractionDirection(), inputHandler.MovementInput.y < 0);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if(isServer)
        {
            HandleDamage(collision);
        }

        if (isOwned && collision.CompareTag("Rope") && inputHandler.MovementInput.y != 0)
        {
            CmdHandleClimbing();
        }

        if (collision.CompareTag("Finish") && inputHandler.MovementInput.y > 0)
        {
            SetFinishState(inputHandler.MovementInput);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOwned)
        {
            if(collision.CompareTag("Rope"))
                CmdEnterRope();
            if (collision.CompareTag("Reset"))
                CmdPositionReset();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (isOwned && collision.CompareTag("Rope"))
        {
            CmdExitClimbing();
        }
    }

    private void FinishCheck(bool oldValue, bool newValue)
    {
        PlayerSetActive(newValue);

        if (newValue)
        {
            GameManager.Instance.FinishCheck();
        }
    }

    private void SetFinishState(Vector2 input)
    {
        if (input.y != 0)
        {
            CmdSetFinishState(input.y > 0);
        }
    }

    [Command]
    private void CmdSetFinishState(bool isFinish)
    {
        this.isFinish = isFinish;
        RpcEnterFinish(isFinish);

        if (isFinish)
        {
            GetComponent<PlayerSound>().RpcPlayEnterSound();
        }
        else
        {
            GetComponent<PlayerSound>().RpcPlayExitSound();
        }
    }

    [ClientRpc]
    private void RpcEnterFinish(bool isFinish)
    {
        this.isFinish = isFinish;
        PlayerSetActive(isFinish);
    }

    private void PlayerSetActive(bool isActive)
    {
        bool value = !isActive;
        nameText.enabled = value;
        spriteRenderer.enabled = value;
        GetComponent<BoxCollider2D>().enabled = value;
        animationController.enabled = value;
        movementHandler.enabled = value;
        stateController.enabled = value;
        interactionController.enabled = value;

        if (value && isOwned)
        {
            cameraController.SetTarget(transform);
            isFinish = false;
        }
    }

    public void SetCarriedState(bool carried, Transform carrier = null)
    {
        isCarried = carried;

        if (carried)
        {
            // 플레이어가 잡힌 상태이므로 움직임, 입력 등을 막아주기
            movementHandler.enabled = false;
            stateController.ChangeState(PlayerState.Carried);

            // 부모를 잡는 캐릭터 transform으로 설정해서 따라다니게 할 수도 있음
            transform.SetParent(carrier);

            // 혹은 collider를 약간 무시처리 해줄 수도 있음(필요시)
            // GetComponent<Collider2D>().enabled = false;
        }
        else
        {
            // 다시 조작 가능
            movementHandler.enabled = true;
            stateController.ChangeState(PlayerState.Idle);

            // 부모 해제
            transform.SetParent(null);

            // Collider 복구(필요시)
            // GetComponent<Collider2D>().enabled = true;
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
