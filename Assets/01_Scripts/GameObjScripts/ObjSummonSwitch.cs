using Mirror;
using UnityEngine;

public class ObjSummonSwitch : NetworkBehaviour
{
    public Transform summonLocation;
    public float cooldownTime = 5f;
    public Transform targetObjTrans;
    public Sprite defaultSprite;
    public Sprite pressedSprite;

    [SyncVar(hook = nameof(OnIsCooldownChanged))] private bool isCooldown;
    [SyncVar] private double cooldownEndsAt;
    private SpriteRenderer spriteRenderer;
    private PickupObj targetBox;

    public bool IsCooldown => isCooldown;
    public double CooldownEndsAt => cooldownEndsAt;

    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    public override void OnStartServer()
    {
        base.OnStartServer();
        targetBox = targetObjTrans != null ? targetObjTrans.GetComponent<PickupObj>() : null;
        if (targetBox == null || summonLocation == null)
            Debug.LogError($"{transform.root.name}/{name}: 상자 또는 소환 위치 참조가 잘못되었습니다.", this);
        ApplyCooldownDisplay();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyCooldownDisplay();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isServer || !collision.CompareTag("Player")) return;
        ServerTrySummon();
    }

    [ServerCallback]
    private void Update()
    {
        if (isServer && isCooldown && NetworkTime.time >= cooldownEndsAt)
        {
            isCooldown = false;
            ApplyCooldownDisplay();
        }
    }

    [Server]
    public bool ServerTrySummon()
    {
        if (!isServer || isCooldown) return false;
        if (targetBox == null || summonLocation == null)
        {
            Debug.LogError($"{transform.root.name}/{name}: 상자 또는 소환 위치 참조가 잘못되었습니다.", this);
            return false;
        }
        cooldownEndsAt = NetworkTime.time + Mathf.Max(0f, cooldownTime);
        isCooldown = true;
        ApplyCooldownDisplay();
        targetBox.ServerReset(summonLocation.position);
        return true;
    }

    private void OnIsCooldownChanged(bool oldValue, bool newValue) => ApplyCooldownDisplay();
    private void ApplyCooldownDisplay() => spriteRenderer.sprite = isCooldown ? pressedSprite : defaultSprite;
}
