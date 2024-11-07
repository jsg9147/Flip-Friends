using UnityEngine;
using Mirror;
using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine.UIElements;

public class PlayerController : NetworkBehaviour
{
    public LayerMask groundLayerMask; // Layer mask for interactable objects
    public TMP_Text nameText;

    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private PlayerInput inputHandler;
    [SerializeField] private PlayerMovement movementHandler;
    [SerializeField] private PlayerBounce bounceHandler;
    [SerializeField] private PlayerInteraction interactioenHandler;
    [SerializeField] private PlayerRopeClimbing climbingHandler;
    [SerializeField] private PlayerStateController stateController;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    private PhysicsMaterial2D originalMaterial;
    private PhysicsMaterial2D zeroFrictionMaterial;

    [SyncVar(hook = nameof(PlayerNameUpdate))] public string playerName = "No Name";

    private PlayerState playerState;

    // 입력 값 저장 변수
    private Vector2 currentMovementInput;
    private bool isRunPressed;
    private bool isJumpHold;
    private bool isPickUpPressed;

    void Start()
    {
        InitializeComponents();
        GenerateZeroFriction();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // 입력을 Update에서 수집하여 저장
        currentMovementInput = inputHandler.MovementInput;
        isRunPressed = inputHandler.IsRunPressed;
        isJumpHold = inputHandler.IsJumpHold;
        isPickUpPressed = inputHandler.IsPickUpPressed;

        HandleUpdateMovement();

        movementHandler.SetJumpingState(DetectGround());
    }

    private void FixedUpdate()
    {
        if (isLocalPlayer)
        {
            if (climbingHandler.isClimbing)
            {
                ClimbInputHandler();
            }
            else
            {
                HandleFixedMovement();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLocalPlayer) return;

        if (collision.transform.CompareTag("Player"))
        {
            bounceHandler.PlayerApplySpringEffect(inputHandler.IsJumpHold);
            bounceHandler.StartShrinking();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Rope"))
        {
            if (inputHandler.MovementInput.y != 0)
            {
                climbingHandler.Climbing(collision.transform);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Rope"))
        {
            if (inputHandler.MovementInput.y != 0)
            {
                climbingHandler.CancelClimbing();
            }
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isLocalPlayer)
        {
            if(MirrorRoomManager.Instance != null)
                CmdSetPlayerName(MirrorRoomManager.Instance.playerName);
        }
    }

    private void InitializeComponents()
    {
        inputHandler = GetComponent<PlayerInput>();
        movementHandler = GetComponent<PlayerMovement>();
        bounceHandler = GetComponent<PlayerBounce>();
        interactioenHandler = GetComponent<PlayerInteraction>();
        climbingHandler = GetComponent<PlayerRopeClimbing>();
        animationController = GetComponent<PlayerAnimationController>();
        stateController = GetComponent<PlayerStateController>();

        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Smooth out physics updates
    }

    private void HandleUpdateMovement()
    {
        if (inputHandler.IsJumpPressed)
        {
            if (climbingHandler.isClimbing)
            {
                climbingHandler.CancelClimbing();

                movementHandler.HandleMovement(currentMovementInput, isRunPressed);
                movementHandler.HandleRopeJump(currentMovementInput);
                stateController.ChangeState(PlayerState.Jump);
                CmdFlipSprite(currentMovementInput);

                animationController.ChangeAnimation(stateController.playerState);
            }
            else
            {
                if (DetectGround() && !interactioenHandler.IsPickUpState)
                {
                    stateController.ChangeState(PlayerState.Jump);
                }

                movementHandler.HandleJump();
                animationController.ChangeAnimation(stateController.playerState);
            }
        }
    }

    private void HandleFixedMovement()
    {
        // FixedUpdate에서 저장된 입력 값을 사용하여 이동 처리
        movementHandler.HandleMovement(currentMovementInput, isRunPressed);
        CmdFlipSprite(currentMovementInput);

        if (isJumpHold)
            movementHandler.HoldJump();

        if (isPickUpPressed)
        {
            Vector3 dir = GetComponent<SpriteRenderer>().flipX ? Vector3.left : Vector3.right;
            interactioenHandler.TryIntractive(dir);
            isPickUpPressed = false; // 한 번만 처리되도록 리셋
        }

        HandleFixedState();
    }

    private void HandleFixedState()
    {
        if (interactioenHandler.IsPickUpState)
        {
            stateController.ChangeState(PlayerState.Carried);
        }
        else if (DetectGround())
        {
            if (currentMovementInput.x != 0f)
            {
                stateController.ChangeState(PlayerState.Walk);
            }
            else
            {
                stateController.ChangeState(PlayerState.Idle);
            }
            animationController.GroundState();
        }

        animationController.ChangeAnimation(stateController.playerState);
    }

    private void ClimbInputHandler()
    {
        climbingHandler.ClimbingMovement(currentMovementInput);
    }

    void GenerateZeroFriction()
    {
        if (boxCollider != null)
        {
            originalMaterial = boxCollider.sharedMaterial;
        }
        zeroFrictionMaterial = new PhysicsMaterial2D("ZeroFriction")
        {
            friction = 0.0f,
            bounciness = originalMaterial != null ? originalMaterial.bounciness : 0.0f
        };
    }

    [Command]
    private void CmdFlipSprite(Vector2 input)
    {
        if (DetectGround())
        {
            bool isFlipped;

            if (input.x < 0)
                isFlipped = true;
            else if (input.x > 0)
                isFlipped = false;
            else
                isFlipped = GetComponent<SpriteRenderer>().flipX;

            FlipSprite(isFlipped);
            RpcFlipSprite(isFlipped);
        }
    }
    [ClientRpc]
    private void RpcFlipSprite(bool isFlipped)
    {
        FlipSprite(isFlipped);
    }

    private void FlipSprite(bool isFlipped)
    {
        GetComponent<SpriteRenderer>().flipX = isFlipped;
    }

    [Command]
    void CmdSetPlayerName(string newName)
    {
        playerName = newName; // 서버에서 플레이어 이름 설정
    }

    private void PlayerNameUpdate(string oldName, string newName)
    {
        nameText.text = newName;
        Debug.Log($"플레이어 이름이 {oldName}에서 {newName}으로 변경되었습니다.");
    }
    bool DetectGround()
    {
        bool isGround = DetectGround(boxCollider, groundLayerMask);
        boxCollider.sharedMaterial = isGround ? originalMaterial : zeroFrictionMaterial;

        return isGround;
    }
    // 바닥 감지 로직
    bool DetectGround(BoxCollider2D boxCollider, LayerMask groundLayerMask)
    {
        if (boxCollider == null) return false;

        Vector2 boxSize = boxCollider.size;
        Vector2 boxCenter = (Vector2)transform.position + boxCollider.offset;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(boxCenter, boxSize, 0f, Vector2.down, 0.05f, groundLayerMask);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject)
            {
                return true;
            }
        }

        return false;
    }
}
