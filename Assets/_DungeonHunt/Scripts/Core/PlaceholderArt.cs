using System.Collections.Generic;
using UnityEngine;

// 최종 아트가 없을 때 쓰는 기본 도형 스프라이트 (09 §3: 리소스 없으면 기본 도형, 색, 선, 점멸로 전달).
// 런타임에 한 번 만들어 캐시함. 아트가 들어오면 프리팹/정의 에셋의 스프라이트를 쓰고 이건 안 씀.
public static class PlaceholderArt
{
    private const int Resolution = 64;
    private static Sprite circle, square, ring, triangle;
    private static readonly Dictionary<Color, Sprite> solidTiles = new Dictionary<Color, Sprite>();
    private static Font uiFont;
    private static Material lineMaterial;

    public static Sprite Circle => circle != null ? circle : circle = CreateSprite(DrawCircle(false));
    public static Sprite Ring => ring != null ? ring : ring = CreateSprite(DrawCircle(true));
    public static Sprite Square => square != null ? square : square = CreateSprite(DrawSquare());
    public static Sprite Triangle => triangle != null ? triangle : triangle = CreateSprite(DrawTriangle());

    // 스프라이트 렌더러/라인 렌더러 공용 기본 머티리얼
    public static Material LineMaterial
    {
        get
        {
            if (lineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                lineMaterial = new Material(shader);
            }
            return lineMaterial;
        }
    }

    // 한글 표시용 OS 폰트. 팀 폰트가 정해지면 GameConfig.UIFont로 덮어씀.
    public static Font UIFont
    {
        get
        {
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "NanumGothic", "Arial" }, 32);
            }
            return uiFont;
        }
        set => uiFont = value;
    }

    // 월드 텍스트용 내장 폰트 (숫자, 영문 위주)
    private static Font worldFont;
    public static Font WorldFont
    {
        get
        {
            if (worldFont != null) return worldFont;
            try { worldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { worldFont = null; }
            if (worldFont == null)
            {
                try { worldFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { worldFont = null; }
            }
            if (worldFont == null) worldFont = UIFont;
            return worldFont;
        }
    }

    public static TextMesh CreateWorldText(string name, Transform parent, string text, float characterSize, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mesh = go.AddComponent<TextMesh>();
        mesh.font = WorldFont;
        mesh.text = text;
        mesh.characterSize = characterSize;
        mesh.fontSize = 48;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mesh.font.material;
        renderer.sortingOrder = sortingOrder;
        return mesh;
    }

    public static SpriteRenderer CreateShape(string name, Transform parent, Sprite sprite, Color color, float diameter, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        go.transform.localScale = Vector3.one * diameter;
        return renderer;
    }

    public static LineRenderer CreateLine(string name, Transform parent, Color color, float width, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.material = LineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;
        line.sortingOrder = sortingOrder;
        line.positionCount = 0;
        return line;
    }

    public static Sprite CreateSprite(Texture2D texture)
        => Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);

    private static Texture2D NewTexture()
    {
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        return texture;
    }

    private static Texture2D DrawCircle(bool hollow)
    {
        Texture2D texture = NewTexture();
        float center = (Resolution - 1) * 0.5f;
        float outer = Resolution * 0.5f;
        float inner = hollow ? outer - Resolution * 0.08f : -1f;
        var pixels = new Color32[Resolution * Resolution];
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(outer - d);
                if (hollow) alpha = Mathf.Min(alpha, Mathf.Clamp01(d - inner));
                pixels[y * Resolution + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawSquare()
    {
        Texture2D texture = NewTexture();
        var pixels = new Color32[Resolution * Resolution];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.filterMode = FilterMode.Point;
        texture.Apply();
        return texture;
    }

    private static Texture2D DrawTriangle()
    {
        Texture2D texture = NewTexture();
        var pixels = new Color32[Resolution * Resolution];
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                // 오른쪽을 가리키는 삼각형
                float t = x / (float)Resolution;
                float halfWidth = (1f - t) * Resolution * 0.5f;
                bool inside = Mathf.Abs(y - Resolution * 0.5f) <= halfWidth;
                pixels[y * Resolution + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
