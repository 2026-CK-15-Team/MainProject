using UnityEngine;

// 원형 몸체를 벽 레이어에 대해 밀어 움직임. 벽에 닿으면 벽 방향 성분만 빼고 표면을 따라 미끄러짐 (01-1 §7.1).
// 플레이어와 몬스터는 서로 막지 않으므로 벽만 검사함.
public static class KinematicMover2D
{
    private const float Skin = 0.01f;
    private const int MaxIterations = 3;
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

    // 실제 이동한 변위를 돌려줌. hitWall: 이동 중 벽에 닿았는지.
    public static Vector2 Move(Vector2 position, float radius, Vector2 delta, out bool hitWall)
    {
        hitWall = false;
        Vector2 start = position;
        int mask = GameLayers.WallMask;

        for (int i = 0; i < MaxIterations && delta.sqrMagnitude > 1e-10f; i++)
        {
            float distance = delta.magnitude;
            Vector2 dir = delta / distance;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };
            int count = Physics2D.CircleCast(position, radius, dir, filter, hits, distance + Skin);
            RaycastHit2D nearest = default;
            float nearestDistance = float.MaxValue;
            for (int h = 0; h < count; h++)
            {
                // 시작부터 겹친 콜라이더(distance 0)는 빠져나가는 방향이면 무시
                if (hits[h].distance <= 0f && Vector2.Dot(hits[h].normal, dir) >= 0f) continue;
                if (hits[h].distance < nearestDistance)
                {
                    nearestDistance = hits[h].distance;
                    nearest = hits[h];
                }
            }

            if (nearest.collider == null)
            {
                position += delta;
                break;
            }

            hitWall = true;
            float travel = Mathf.Max(0f, nearestDistance - Skin);
            position += dir * travel;
            Vector2 remaining = delta - dir * travel;
            // 벽 방향 성분 제거
            remaining -= nearest.normal * Vector2.Dot(remaining, nearest.normal);
            delta = remaining;
        }
        return position - start;
    }

    // 넉백처럼 벽에 닿으면 즉시 멈추는 이동 (04 §9.1)
    public static Vector2 MoveUntilWall(Vector2 position, float radius, Vector2 delta, out bool hitWall)
    {
        hitWall = false;
        float distance = delta.magnitude;
        if (distance < 1e-6f) return Vector2.zero;
        Vector2 dir = delta / distance;
        RaycastHit2D hit = Physics2D.CircleCast(position, radius, dir, distance + Skin, GameLayers.WallMask);
        if (hit.collider == null) return delta;
        hitWall = true;
        return dir * Mathf.Max(0f, hit.distance - Skin);
    }
}
