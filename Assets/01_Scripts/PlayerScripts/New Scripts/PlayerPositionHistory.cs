using UnityEngine;

/// <summary>
/// 서버가 플레이어 위치를 서버 시각과 함께 기록해 둔다.
/// 지연 보상에서 원격 클라이언트가 보던 시점의 위치를 꺼낼 때 쓴다.
/// </summary>
public class PlayerPositionHistory
{
    private readonly double[] times;
    private readonly Vector2[] positions;
    private int nextIndex;
    private int count;

    public PlayerPositionHistory(int capacity)
    {
        times = new double[capacity];
        positions = new Vector2[capacity];
    }

    private int Capacity => times.Length;
    private int IndexFromNewest(int offset) => (nextIndex - 1 - offset + Capacity) % Capacity;

    public void Record(double time, Vector2 position)
    {
        // 같은 프레임에 두 번 기록되면 NetworkTransform이 보낸 것과 같은 마지막 값으로 덮는다.
        if (count > 0 && times[IndexFromNewest(0)] == time)
        {
            positions[IndexFromNewest(0)] = position;
            return;
        }

        times[nextIndex] = time;
        positions[nextIndex] = position;
        nextIndex = (nextIndex + 1) % Capacity;
        count = Mathf.Min(count + 1, Capacity);
    }

    // time이 마지막 기록 이후면 지금 위치를 그대로 쓰면 되므로 false를 돌려준다.
    // 원격 화면도 스냅샷 사이를 선형 보간해 그리므로 기록 사이도 선형 보간한다.
    public bool TrySample(double time, out Vector2 position)
    {
        position = default;
        if (count == 0 || time >= times[IndexFromNewest(0)]) return false;

        int newer = IndexFromNewest(0);
        for (int i = 1; i < count; i++)
        {
            int older = IndexFromNewest(i);
            if (times[older] <= time)
            {
                float t = (float)((time - times[older]) / (times[newer] - times[older]));
                position = Vector2.Lerp(positions[older], positions[newer], t);
                return true;
            }
            newer = older;
        }

        // 기록보다 오래된 시점은 남아 있는 가장 오래된 위치로 대신한다.
        position = positions[newer];
        return true;
    }
}
