using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
using TMPro;

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
    private ServerMover serverMover;
    private NetworkTransformUnreliable networkTransform;
    private ThrownPlayerSmoother thrownSmoother;

    // 운반자는 서버가 정하고 RPC로 모든 클라이언트에 알린다. 게임 중에는 새로 들어오는 클라이언트가 없어 SyncVar가 필요 없다.
    private PlayerInteraction carrier;
    public bool isCarried => carrier != null;
    public bool CanBeCarried => !isCarried && !isFinish && !interactionController.IsHoldingAnything;

    // 들린 동안 조작 입력을 누르고 있으면 이 간격으로 탈출을 요청한다. 고정 시간 판정은 서버가 한다.
    private const float EscapeRequestInterval = 0.25f;
    private const float SteppedJumpBlockDuration = 0.15f;
    private float escapeRequestCooldown;

    private SavePoint savePoint;

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
        Debug.Log($"{playerName} is ready: {oldValue} -> {newValue}");
    }

    private void PlayerNameUpdate(string oldName, string newName)
    {
        nameText.text = newName;
        Debug.Log($"Player Name changed from {oldName} to {newName}.");
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
        serverMover = GetComponent<ServerMover>();
        networkTransform = GetComponent<NetworkTransformUnreliable>();
        thrownSmoother = GetComponent<ThrownPlayerSmoother>();
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
        else if (isCarried)
        {
            HandleCarriedInput();
        }
        else
        {
            // 이동/점프는 ClientMover가 담당 — 여기서는 게임 액션만 처리.
            // 아래 입력은 서버에 없으므로 누른 순간의 값을 함께 보낸다.
            if (inputHandler.ConsumePickUpPressed())
                CmdObjectInteraction(inputHandler.MovementInput.y < 0);

            if (inputHandler.ConsumeResetPressed())
                CmdPositionReset();
        }
    }

    private void HandleCarriedInput()
    {
        // 들린 동안 누른 들기·리셋은 풀려난 뒤 갑자기 실행되지 않도록 버린다.
        inputHandler.ConsumePickUpPressed();
        inputHandler.ConsumeResetPressed();

        escapeRequestCooldown -= Time.deltaTime;
        bool wantsEscape = inputHandler.MovementInput != Vector2.zero || inputHandler.IsJumpHold;
        if (!wantsEscape || escapeRequestCooldown > 0f) return;

        escapeRequestCooldown = EscapeRequestInterval;
        CmdRequestEscape();
    }

    [Command]
    private void CmdRequestEscape()
    {
        if (carrier != null)
            carrier.TryEscape(this);
    }

    [Server]
    public void BeginCarried(PlayerInteraction by)
    {
        carrier = by;
        ApplyCarried(by);
        RpcBeginCarried(by.netIdentity);
    }

    [Server]
    public void EndCarried(Vector2 position, Vector2 velocity, float uncontrollableDuration)
    {
        carrier = null;
        ApplyReleased(position, velocity);
        RpcEndCarried(position, velocity);
        // RPC 뒤에 보내야 소유 클라이언트가 부모에서 떨어진 다음 새 예측을 시작한다.
        serverMover.Teleport(position, velocity, uncontrollableDuration);
    }

    [ClientRpc]
    private void RpcBeginCarried(NetworkIdentity carrierIdentity)
    {
        if (isServer || carrierIdentity == null) return;
        carrier = carrierIdentity.GetComponent<PlayerInteraction>();
        ApplyCarried(carrier);
    }

    [ClientRpc]
    private void RpcEndCarried(Vector2 position, Vector2 velocity)
    {
        if (isServer) return;
        carrier = null;
        ApplyReleased(position, velocity);
    }

    // 모든 화면에서 운반자의 자식으로 붙인다. 각 화면이 보여 주는 운반자 머리 위에 붙으므로 화면끼리 어긋나지 않는다.
    private void ApplyCarried(PlayerInteraction by)
    {
        transform.SetParent(by.transform, false);
        transform.localPosition = by.HeldOffset;
        movementHandler.Launch(Vector2.zero, 0f);
        movementHandler.enabled = false;
        thrownSmoother.Cancel();

        // 서버는 계속 보내야 하고, 클라이언트는 부모를 따라가야 하므로 클라이언트 쪽 보간만 끈다.
        if (!isServer)
            networkTransform.enabled = false;
    }

    private void ApplyReleased(Vector2 position, Vector2 velocity)
    {
        Vector2 shownPosition = transform.position;
        transform.SetParent(null, true);
        transform.position = position;
        movementHandler.enabled = true;

        if (isServer) return;

        // 운반 중에 쌓인 부모 기준 스냅샷으로 보간하지 않게 비운다.
        networkTransform.ResetState();
        networkTransform.enabled = true;

        // 내 캐릭터는 ClientMover가 따라가게 하고, 남의 캐릭터는 보이던 자리에서 던진 궤적을 그려 보간 위치로 넘긴다.
        if (!isOwned)
            thrownSmoother.Begin(shownPosition, position, velocity);
    }

    // 운반자가 먼저 파괴되는 클라이언트에서 같이 파괴되지 않게 떼어 둔다. 위치·이동은 이어서 오는 서버 RPC가 정한다.
    public void DetachFromCarrierLocally()
    {
        if (transform.parent != null)
            transform.SetParent(null, true);
    }

    public override void OnStopServer()
    {
        if (carrier != null)
            carrier.ForgetHeldPlayer(this);
        base.OnStopServer();
    }

    private void HandleFinishState()
    {
        if (inputHandler.MovementInput.y < 0)
        {
            SetFinishState(inputHandler.MovementInput);
        }
    }

    [Server]
    public void OnSteppedByOtherPlayer()
    {
        stateController.RpcPlayOneShot(PlayerState.Shrink);
        soundController.RpcPlayShrinkSound();
        movementHandler.BlockJump(SteppedJumpBlockDuration);
        serverMover.SendStateNow();
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
        // 들린 동안의 위치는 운반자가 정한다.
        if (isCarried) return;

        interactionController.DropAll();
        Vector2 resetPos = Vector2.zero;
        if(savePoint != null)
            resetPos = savePoint.transform.position;
        serverMover.Teleport(resetPos, Vector2.zero);
    }

    private void UpdatePlayerState()
    {
        if (movementHandler == null) return;

        bool isGrounded = movementHandler.isGrounded;
        Vector2 velocity = movementHandler.CurrentVelocity;

        if (!isCarried && isGrounded && PlayerStateController.IsWalking(velocity.x))
            UpdateFlipState(velocity.x < 0);

        // 매 틱 이동 결과에서 상태를 다시 정한다. SyncVar라 값이 바뀔 때만 전송된다
        stateController.ChangeState(ResolveServerState(isGrounded, velocity));
    }

    private PlayerState ResolveServerState(bool isGrounded, Vector2 velocity)
    {
        if (isCarried) return PlayerState.Carried;
        if (movementHandler.isClimbed && !isGrounded) return PlayerStateController.ResolveClimb(velocity);
        return PlayerStateController.ResolveLocomotion(isGrounded, velocity.x);
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

    private void HandleDamage(Collider2D collision)
    {
        if (!collision.CompareTag("Trap") && !collision.CompareTag("Enemy")) return;
        // 들린 동안은 이동이 꺼져 있어 넉백을 줄 수 없다. 운반자가 맞으면 그쪽에서 내려놓는다.
        if (isCarried) return;

        Vector2 knockbackDirection = (transform.position - collision.transform.position).normalized;
        BasicTrap trap = collision.GetComponent<BasicTrap>();

        if (trap != null && trap.knockbackDir != Vector2.zero)
        {
            knockbackDirection = trap.knockbackDir;
        }

        if (!movementHandler.OnDamaged(knockbackDirection)) return;

        interactionController.DropAll();
        stateController.RpcPlayOneShot(PlayerState.Damaged);
        // 피격은 서버만 판정하므로 소유 클라이언트가 넉백을 늦지 않게 받도록 바로 보낸다.
        serverMover.SendStateNow();
    }

    public Vector2 FacingDirection => spriteRenderer.flipX ? Vector2.left : Vector2.right;

    [Command]
    private void CmdObjectInteraction(bool isPutDown)
    {
        interactionController.TryIntractive(FacingDirection, isPutDown);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if(isServer)
        {
            HandleDamage(collision);
        }

        if (!isCarried && collision.CompareTag("Finish") && inputHandler.MovementInput.y > 0)
        {
            SetFinishState(inputHandler.MovementInput);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isOwned && collision.CompareTag("Reset"))
            CmdPositionReset();
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
        if (isCarried) return;

        // 숨겨진 채로 들고 있으면 들린 플레이어가 보이지 않는 운반자에 붙어 있게 된다.
        if (isFinish)
            interactionController.DropAll();

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
}
