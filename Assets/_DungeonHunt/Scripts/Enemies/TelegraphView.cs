using System.Collections.Generic;
using UnityEngine;

// 공격 예고 표시 (09 §7). 아트 없이 선과 부채꼴로 방향과 범위를 전달함.
public class TelegraphView
{
    private readonly Transform owner;
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private Vector3 baseScale = Vector3.one;
    private SpriteRenderer pulsed;

    public TelegraphView(Transform owner)
    {
        this.owner = owner;
    }

    private LineRenderer GetLine(int index)
    {
        while (lines.Count <= index)
            lines.Add(PlaceholderArt.CreateLine("Telegraph", owner, Color.red, 0.1f, 250));
        return lines[index];
    }

    public void ShowLine(Vector2 from, Vector2 to, Color color, float width, bool withEndPoint)
    {
        HideFrom(withEndPoint ? 2 : 1);
        LineRenderer line = GetLine(0);
        SetLine(line, color, width, from, to);
        if (withEndPoint)
        {
            // 끝점 표시: 짧은 가로선
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * 0.25f;
            SetLine(GetLine(1), color, width, to - perp, to + perp);
        }
    }

    public void ShowSegments(List<(Vector2 from, Vector2 to)> segments, Color color, float width)
    {
        HideFrom(segments.Count);
        for (int i = 0; i < segments.Count; i++)
            SetLine(GetLine(i), color, width, segments[i].from, segments[i].to);
    }

    public void ShowSector(Vector2 origin, Vector2 dir, float range, float arcDeg, Color color)
    {
        LineRenderer line = GetLine(0);
        HideFrom(1);
        const int arcPoints = 12;
        line.positionCount = arcPoints + 3;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.loop = false;
        line.SetPosition(0, origin);
        float half = arcDeg * 0.5f;
        for (int i = 0; i <= arcPoints; i++)
        {
            float a = -half + arcDeg * i / arcPoints;
            line.SetPosition(i + 1, origin + Geometry2D.Rotate(dir.normalized, a) * range);
        }
        line.SetPosition(arcPoints + 2, origin);
        line.enabled = true;
    }

    public void Pulse(SpriteRenderer renderer, float time)
    {
        if (renderer == null) return;
        if (pulsed != renderer)
        {
            pulsed = renderer;
            baseScale = renderer.transform.localScale;
        }
        renderer.transform.localScale = baseScale * (1f + 0.12f * Mathf.Abs(Mathf.Sin(time * 12f)));
    }

    public void Hide()
    {
        HideFrom(0);
        if (pulsed != null)
        {
            pulsed.transform.localScale = baseScale;
            pulsed = null;
        }
    }

    private void HideFrom(int index)
    {
        for (int i = index; i < lines.Count; i++)
        {
            if (lines[i] != null) lines[i].enabled = false;
        }
    }

    private static void SetLine(LineRenderer line, Color color, float width, Vector2 from, Vector2 to)
    {
        line.enabled = true;
        line.loop = false;
        line.positionCount = 2;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
    }
}
