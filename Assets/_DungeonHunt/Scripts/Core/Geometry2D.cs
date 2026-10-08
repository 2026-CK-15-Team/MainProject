using UnityEngine;

// 판정용 순수 수학. 물리 엔진 콜백 순서에 결과가 흔들리지 않게 몬스터·플레이어 판정은 이걸로 함.
public static class Geometry2D
{
    // 반지름 r로 a→b를 쓸고 지나갈 때 중심 c, 반지름 R인 원에 처음 닿는 비율(0~1). 안 닿으면 -1.
    // 시작부터 겹쳐 있으면 0.
    public static float SweepCircle(Vector2 a, Vector2 b, float r, Vector2 c, float targetRadius)
    {
        float radius = r + targetRadius;
        Vector2 d = b - a;
        Vector2 f = a - c;
        float cc = Vector2.Dot(f, f) - radius * radius;
        if (cc <= 0f) return 0f;

        float aa = Vector2.Dot(d, d);
        if (aa < 1e-8f) return -1f;

        float bb = 2f * Vector2.Dot(f, d);
        float disc = bb * bb - 4f * aa * cc;
        if (disc < 0f) return -1f;

        float t = (-bb - Mathf.Sqrt(disc)) / (2f * aa);
        return t >= 0f && t <= 1f ? t : -1f;
    }

    // 선분과 원이 겹치는지 (경계 접촉 포함)
    public static bool SegmentIntersectsCircle(Vector2 a, Vector2 b, Vector2 c, float radius)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        float t = lengthSq < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(c - a, ab) / lengthSq);
        Vector2 closest = a + ab * t;
        return (c - closest).sqrMagnitude <= radius * radius;
    }

    // 꼭짓점 origin, 방향 dir, 반경 range, 전체 각 arcDeg인 부채꼴이 원(c, r)과 겹치는지. 경계 접촉 포함.
    public static bool SectorIntersectsCircle(Vector2 origin, Vector2 dir, float range, float arcDeg, Vector2 c, float r)
    {
        Vector2 toTarget = c - origin;
        float dist = toTarget.magnitude;
        if (dist - r > range) return false;
        if (dist <= r) return true;

        float halfArc = arcDeg * 0.5f;
        float angle = Vector2.Angle(dir, toTarget);
        if (angle <= halfArc) return true;

        // 부채꼴 경계선 두 개와 원 접촉 검사
        Vector2 left = Rotate(dir.normalized, halfArc) * range;
        Vector2 right = Rotate(dir.normalized, -halfArc) * range;
        return SegmentIntersectsCircle(origin, origin + left, c, r) || SegmentIntersectsCircle(origin, origin + right, c, r);
    }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    public static Vector2 FromAngle(float degrees) => Rotate(Vector2.right, degrees);

    public static bool HasLineOfSight(Vector2 from, Vector2 to)
        => Physics2D.Linecast(from, to, GameLayers.WallMask).collider == null;
}
