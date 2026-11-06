using System;
using System.Collections.Generic;
using UnityEngine;

// 속도는 기존과 같이 거리/초가 아니라 구간 진행률/초다.
public static class PlatformMotion
{
    private const int MaxLegacySegmentTicks = 10000;
    private static readonly Dictionary<(float speed, float step), double> durations = new Dictionary<(float, float), double>();
    public struct Sample
    {
        public Vector3 offset;
        public bool isCompleted;
        public bool hasReturned;
        public long cycle;
    }

    public static Sample Evaluate(Vector3[] points, float speed, float wait, bool cyclic,
        bool repeat, float returnDelay, double elapsed, float easeAmount = 0f)
    {
        var sample = new Sample { offset = points != null && points.Length > 0 ? points[0] : Vector3.zero };
        if (points == null || points.Length < 2 || speed <= 0f || elapsed <= 0) return sample;
        double step = Time.fixedDeltaTime;
        // 기존 FixedUpdate의 마지막 진행률 반올림과 대기 틱을 보존한다.
        double duration = SegmentDuration(speed, Time.fixedDeltaTime);
        double waitDuration = Math.Max(0, Math.Ceiling(wait / step - .00001) - 1) * step;
        double legDuration = duration + waitDuration;
        int legs = points.Length - 1;
        double endTime = legs * legDuration - waitDuration;
        if (!cyclic && !repeat && elapsed >= endTime)
        {
            sample.isCompleted = true;
            sample.hasReturned = elapsed >= endTime + Math.Max(Time.fixedDeltaTime, returnDelay);
            sample.offset = sample.hasReturned ? points[0] : points[legs];
            return sample;
        }
        double routeDuration = legs * legDuration;
        long route = Math.Max(0, (long)Math.Floor((elapsed - .000000001) / routeDuration));
        double routeTime = elapsed - route * routeDuration;
        int leg = Math.Min(legs - 1, (int)(routeTime / legDuration));
        double phase = routeTime - leg * legDuration;
        bool reverse = cyclic && (route & 1) != 0;
        int from = reverse ? legs - leg : leg;
        int to = reverse ? from - 1 : from + 1;
        sample.offset = Vector3.LerpUnclamped(points[from], points[to], Ease((float)Math.Min(1, phase * speed), easeAmount));
        sample.cycle = cyclic ? 0 : route;
        return sample;
    }

    private static double SegmentDuration(float speed, float step)
    {
        if (durations.TryGetValue((speed, step), out double duration)) return duration;
        float progress = 0;
        // 실제 스테이지 속도에서는 수백 틱 이내다. float 누적 오차로 추가되던 마지막 틱도 보존한다.
        for (int ticks = 1; ticks <= MaxLegacySegmentTicks; ticks++)
        {
            progress = Mathf.Clamp01(progress + step * speed);
            if (progress < 1) continue;
            duration = ticks * (double)step;
            durations[(speed, step)] = duration;
            return duration;
        }
        duration = Math.Ceiling(1.0 / (speed * step)) * step;
        durations[(speed, step)] = duration;
        return duration;
    }

    public static float Ease(float t, float amount)
    {
        float a = amount + 1;
        return Mathf.Pow(t, a) / (Mathf.Pow(t, a) + Mathf.Pow(1 - t, a));
    }
}
