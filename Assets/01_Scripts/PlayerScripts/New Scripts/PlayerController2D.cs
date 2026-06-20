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

    // 이전 프레임의 접지 상태 — 이륙 순간 점프 애니메이션 자동 트리거에 사용
    private bool wasGrounded = true;

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
        Debug.Log($"{playerName}�� �غ���°� {oldValue} ���� {newValue} �Ǿ����ϴ�");
    }

    private void PlayerNameUpdate(string oldName, string newName)
    {
        nameText.text = newName;
        Debug.Log($"�÷��̾� �̸��� {oldName}���� {newName}���� ����Ǿ����ϴ�.");
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
            // 이동/점프는 ClientMover가 담당 — 여기서는 게임 액션만 처리
            if (inputHandler.IsPickUpPressed)
                CmdObjectInteraction();

            if (inputHandler.ResetPressed)
                CmdPositionReset();
        }
    }

    private void HandleFinishState()
    {
        if (inputHandler.MovementInput.y < 0)
        {
            SetFinishState(inputHandler.MovementInput);
        }
    }

    public void OnSteppedByOtherPlayer()
    {
        UpdatePlayerStateAndAnimation(PlayerState.Shrink);
        movementHandler.BlockJump(0.15f);
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
        // ClientMover의 predictedPosition도 동기화 — 리셋 후 LateUpdate가 이전 위치로 덮어쓰는 현상 방지
        GetComponent<ClientMover>().RpcForcePositionSync(resetPos);
        GetComponent<ServerMover>().ClearInputQueue();
    }

    private void UpdatePlayerState()
    {
        if (movementHandler == null) return;

        bool isGrounded = movementHandler.isGrounded;

        if (isGrounded)
        {
            UpdateGroundedState();
            wasGrounded = true;
        }
        else if (wasGrounded)
        {
            // 이번 프레임에 처음 공중으로 전환 — 점프/낙하 모두 Jump 애니메이션으로 처리
            UpdatePlayerStateAndAnimation(PlayerState.Jump);
            wasGrounded = false;
        }

        animationController.RpcGroundState(isGrounded);
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
        if (interactionController.IsHoldingObject) return;

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

        // 씬 전환 판정은 서버 한 곳에서만 — 클라이언트마다 중복 호출 방지
        if (newValue && isServer)
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
        // SyncVar hook(FinishCheck)이 자동으로 모든 클라이언트에 PlayerSetActive를 호출 — 별도 RPC 불필요

        if (isFinish)
            GetComponent<PlayerSound>().RpcPlayEnterSound();
        else
            GetComponent<PlayerSound>().RpcPlayExitSound();
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
        NetworkIdentity carrierIdentity = carrier != null ? carrier.GetComponent<NetworkIdentity>() : null;
        RpcSetCarriedState(carried, carrierIdentity);
    }

    [ClientRpc]
    private void RpcSetCarriedState(bool carried, NetworkIdentity carrierIdentity)
    {
        movementHandler.enabled = !carried;
        stateController.ChangeState(carried ? PlayerState.Carried : PlayerState.Idle);
        transform.SetParent(carried && carrierIdentity != null ? carrierIdentity.transform : null);
    }
}
