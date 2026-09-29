using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class EntrySafeZoneValidator : MonoBehaviour
{
    [SerializeField] private Transform center;                    // 플레이어가 처음 통과하는 문의 안쪽 중심
    [SerializeField] private float radius = 2.0f;                 // 진입 안전 구역 반경 (H)
    [SerializeField] private List<Collider2D> obstacles = new();  // 룸 안쪽에 배치한 기둥/벽 (룸 외곽 벽은 넣지 않는다)

    public void Validate(IEnumerable<Transform> monsters, string roomName)
    {
        if (center == null)
        {
            Debug.LogWarning($"[SafeZone] {roomName}: Center가 비어있어 검증하지 못함");
            PlaytestLogger.Log("SafeZoneCheck", $"room={roomName},result=NO_CENTER");
            return;
        }

        Vector2 c = center.position;
        int violations = 0;
        var detail = new StringBuilder();

        float minMonster = float.MaxValue;
        foreach (var monster in monsters)
        {
            float d = Vector2.Distance(c, monster.position);
            minMonster = Mathf.Min(minMonster, d);

            if (d < radius)
            {
                violations++;
                detail.Append($",monster:{monster.name}={d:F2}");
            }
        }

        float minObstacle = float.MaxValue;
        foreach (var obstacle in obstacles)
        {
            if (obstacle == null) continue;

            float d = Vector2.Distance(c, obstacle.ClosestPoint(c));
            minObstacle = Mathf.Min(minObstacle, d);

            if (d < radius)
            {
                violations++;
                detail.Append($",obstacle:{obstacle.name}={d:F2}");
            }
        }

        string result = violations == 0 ? "PASS" : "FAIL";
        PlaytestLogger.Log("SafeZoneCheck",
            $"room={roomName},radius={radius},minMonsterDistance={Format(minMonster)}," +
            $"minObstacleDistance={Format(minObstacle)},violations={violations},result={result}{detail}");

        if (violations > 0)
            Debug.LogWarning($"[SafeZone] {roomName}: 진입 안전 구역 위반 {violations}건{detail}");
    }

    private static string Format(float value) => value == float.MaxValue ? "n/a" : value.ToString("F2");

    private void OnDrawGizmosSelected()
    {
        if (center == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(center.position, radius);
    }
}
