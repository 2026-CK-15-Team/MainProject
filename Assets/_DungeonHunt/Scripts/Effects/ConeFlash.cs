using UnityEngine;

public class ConeFlash : MonoBehaviour
{
    [SerializeField] private LineRenderer outline;
    [SerializeField] private float duration = 0.1f;
    private const int ArcSegments = 8;

    public void Show(Vector2 origin, Vector2 direction, float range, float coneAngleDegrees)
    {
        float half = coneAngleDegrees * 0.5f;

        outline.loop = true;
        outline.positionCount = ArcSegments + 1;
        outline.SetPosition(0, origin);

        for (int i = 0; i <= ArcSegments - 1; i++)
        {
            float t = (float)i / (ArcSegments - 1);
            float angle = Mathf.Lerp(half, -half, t);
            Vector2 dir = Quaternion.Euler(0, 0, angle) * direction;
            outline.SetPosition(i + 1, origin + dir * range);
        }

        Destroy(gameObject, duration);
    }
}
