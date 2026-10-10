using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 원격 클라이언트의 입력을 서버가 시뮬레이션하는 동안 다른 플레이어를 그 클라이언트가 보던 위치로 잠깐 옮긴다.
/// 원격 플레이어는 NetworkTransform 보간 때문에 과거 시점에 그려지므로, 서버 현재 위치로 판정하면
/// 움직이는 상대의 머리 밟기가 그 클라이언트 화면과 어긋나 보정이 반복된다.
/// 옮기는 대상은 플레이어뿐이고, 같은 틱 안에서 반드시 Restore로 되돌린다.
/// </summary>
public static class PlayerLagCompensation
{
    // 이보다 오래된 시점을 보고 있다고 주장하는 입력은 이 시점으로 자른다.
    public const double MaxRewindTime = 0.5;

    private static readonly List<ServerMover> movers = new List<ServerMover>();
    private static readonly List<Transform> rewoundTransforms = new List<Transform>();
    private static readonly List<Vector3> originalPositions = new List<Vector3>();

    public static void Register(ServerMover mover)
    {
        if (!movers.Contains(mover))
            movers.Add(mover);
    }

    public static void Unregister(ServerMover mover) => movers.Remove(mover);

    public static void Rewind(ServerMover simulating, double viewTime)
    {
        if (rewoundTransforms.Count > 0)
        {
            Debug.LogError($"[{simulating.name}] 이전 되돌리기가 복원되지 않은 채 다시 되돌리려 했습니다. 먼저 복원합니다.", simulating);
            Restore();
        }

        double now = NetworkTime.localTime;
        viewTime = System.Math.Clamp(viewTime, now - MaxRewindTime, now);

        foreach (ServerMover mover in movers)
        {
            if (mover == simulating || mover == null) continue;
            if (!mover.TryGetRewindPosition(viewTime, out Vector2 rewindPosition)) continue;

            Transform target = mover.transform;
            rewoundTransforms.Add(target);
            originalPositions.Add(target.position);
            target.position = new Vector3(rewindPosition.x, rewindPosition.y, target.position.z);
        }

        // Auto Sync Transforms가 꺼져 있어 레이캐스트가 옮긴 위치를 보려면 직접 맞춰야 한다.
        if (rewoundTransforms.Count > 0)
            Physics2D.SyncTransforms();
    }

    public static void Restore()
    {
        if (rewoundTransforms.Count == 0) return;

        for (int i = 0; i < rewoundTransforms.Count; i++)
        {
            if (rewoundTransforms[i] != null)
                rewoundTransforms[i].position = originalPositions[i];
        }

        rewoundTransforms.Clear();
        originalPositions.Clear();
        Physics2D.SyncTransforms();
    }
}
