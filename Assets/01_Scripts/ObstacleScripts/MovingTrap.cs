using UnityEngine;
using Mirror;

// 위치·회전을 시작 시각으로부터의 경과 시간만으로 계산한다.
// 서버와 클라이언트가 같은 식을 각자 풀기 때문에 매 틱 위치를 보내지 않아도 되고, 늦게 들어와도 위상이 맞는다.
public class MovingTrap : BasicTrap
{
    private const float MinPathLength = 0.0001f;
    private const double DegreesPerTurn = 360.0;

    [SerializeField] private Vector2 endPosition;
    [SerializeField] private float speed = 2f;
    [SerializeField] private bool isRotate = false;
    [SerializeField] private float rotationSpeed = 180f;

    [SyncVar] private double startTime;

    private Vector2 startPosition;
    private float startAngle;

    private void Awake()
    {
        startPosition = transform.position;
        startAngle = transform.eulerAngles.z;
    }

    public override void OnStartServer()
    {
        startTime = NetworkTime.localTime;
    }

    private void Update()
    {
        if (!isServer && !isClient) return;

        // 피격은 서버가 소유 클라이언트의 입력을 처리하는 시점에 판정한다.
        // predictedTime은 지금 보낸 입력이 서버에 닿을 시각이라, 내 화면의 트랩 위치가 그 판정 위치와 같아진다. 서버에서는 localTime이다.
        double elapsed = NetworkTime.predictedTime - startTime;

        Vector2 position = EvaluatePosition(elapsed);
        transform.position = new Vector3(position.x, position.y, transform.position.z);
        if (isRotate)
            transform.rotation = Quaternion.Euler(0f, 0f, EvaluateAngle(elapsed));
    }

    // MoveTowards로 왕복하던 이전 동작과 같은 경로를 시간의 함수로 푼다.
    private Vector2 EvaluatePosition(double elapsed)
    {
        float pathLength = endPosition.magnitude;
        if (pathLength < MinPathLength || speed <= 0f) return startPosition;

        double roundTrip = pathLength * 2.0;
        double travelled = (elapsed * speed) % roundTrip;
        if (travelled < 0) travelled += roundTrip;

        float distanceFromStart = (float)(travelled <= pathLength ? travelled : roundTrip - travelled);
        return startPosition + endPosition / pathLength * distanceFromStart;
    }

    private float EvaluateAngle(double elapsed)
    {
        // 서버가 오래 켜져 있어도 float 정밀도를 잃지 않도록 double에서 한 바퀴 단위로 자른다.
        return startAngle + (float)((rotationSpeed * elapsed) % DegreesPerTurn);
    }

    private void OnDrawGizmos()
    {
        Vector2 globalStartPosition = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Vector2 globalEndPosition = globalStartPosition + endPosition;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(globalStartPosition, globalEndPosition);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(globalEndPosition, 0.1f);
    }
}
