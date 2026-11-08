namespace Quantum
{
    using Photon.Deterministic;

    [System.Serializable]
    public sealed class FlipFriendsTerrainContour
    {
        public string Id;
        public FPVector2[] Vertices;
    }

    public static class FlipFriendsTerrain
    {
        public static bool Overlaps(FlipFriendsTerrainContour[] contours, FPVector2 center, FPVector2 half)
        {
            if (contours == null) return false;
            bool inside = false;
            foreach (var contour in contours)
            {
                var v = contour.Vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    var a = v[i]; var b = v[(i + 1) % v.Length];
                    long dx = b.X.RawValue - a.X.RawValue, dy = b.Y.RawValue - a.Y.RawValue;
                    long cx = center.X.RawValue - a.X.RawValue, cy = center.Y.RawValue - a.Y.RawValue;
                    if ((a.Y > center.Y) != (b.Y > center.Y) && (dy > 0 ? cx * dy < cy * dx : cx * dy > cy * dx)) inside = !inside;
                    if (FPMath.Min(a.X, b.X) < center.X + half.X && FPMath.Max(a.X, b.X) > center.X - half.X &&
                        FPMath.Min(a.Y, b.Y) < center.Y + half.Y && FPMath.Max(a.Y, b.Y) > center.Y - half.Y &&
                        System.Math.Abs(dx * cy - dy * cx) < System.Math.Abs(dx) * half.Y.RawValue + System.Math.Abs(dy) * half.X.RawValue) return true;
                }
            }
            return inside;
        }

        public static FP Limit(FlipFriendsTerrainContour[] contours, FPVector2 center, FPVector2 half, FP amount, bool horizontal)
        {
            if (contours == null || amount == 0) return amount;
            FP along = horizontal ? center.X : center.Y, across = horizontal ? center.Y : center.X;
            FP extent = horizontal ? half.X : half.Y, width = horizontal ? half.Y : half.X;
            FP result = amount;
            foreach (var contour in contours)
            {
                var v = contour.Vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    var a = v[i]; var b = v[(i + 1) % v.Length];
                    if (!Range(horizontal ? a.Y : a.X, horizontal ? b.Y : b.X, across - width, across + width,
                        horizontal ? a.X : a.Y, horizontal ? b.X : b.Y, out var min, out var max)) continue;
                    FP near = amount > 0 ? min : max;
                    FP distance = near - along - (amount > 0 ? extent : -extent);
                    if (amount > 0 && distance >= 0) result = FPMath.Min(result, distance);
                    if (amount < 0 && distance <= 0) result = FPMath.Max(result, distance);
                }
            }
            return result;
        }

        // 매개변수를 FP로 먼저 반올림하면 미세 기울기의 천장에서 1raw를 관통할 수 있어 정수 유리수로 자른다.
        private static bool Range(FP a, FP b, FP min, FP max, FP x, FP y, out FP low, out FP high)
        {
            low = high = 0;
            if (a == b) { low = FPMath.Min(x, y); high = FPMath.Max(x, y); return a > min && a < max; }
            if (a > b) { var swap = a; a = b; b = swap; swap = x; x = y; y = swap; }
            if (b <= min || a >= max) return false;
            long start = FPMath.Max(a, min).RawValue - a.RawValue, end = FPMath.Min(b, max).RawValue - a.RawValue;
            long delta = y.RawValue - x.RawValue, denominator = b.RawValue - a.RawValue;
            long n0 = delta * start, n1 = delta * end;
            low = FP.FromRaw(x.RawValue + Floor(System.Math.Min(n0, n1), denominator));
            high = FP.FromRaw(x.RawValue - Floor(-System.Math.Max(n0, n1), denominator));
            return true;
        }
        private static long Floor(long numerator, long denominator) => numerator / denominator - (numerator < 0 && numerator % denominator != 0 ? 1 : 0);
    }
}
