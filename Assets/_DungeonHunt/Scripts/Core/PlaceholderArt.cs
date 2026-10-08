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

    // 아이콘용 실루엣 (아트 없을 때 HUD/월드 오브젝트 구분용)
    private static Sprite lockIcon, heart, diamond, chest, portal, rifle, pistol, shotgun, coin;
    public static Sprite Lock => lockIcon != null ? lockIcon : lockIcon = CreateSprite(DrawMask(LockShape));
    public static Sprite Heart => heart != null ? heart : heart = CreateSprite(DrawMask(HeartShape));
    public static Sprite Diamond => diamond != null ? diamond : diamond = CreateSprite(DrawMask((u, v) => Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) <= 0.48f));
    public static Sprite Chest => chest != null ? chest : chest = CreateSprite(DrawMask(ChestShape));
    public static Sprite Portal => portal != null ? portal : portal = CreateSprite(DrawMask(PortalShape));
    public static Sprite Coin => coin != null ? coin : coin = CreateSprite(DrawMask(CoinShape));

    public static Sprite WeaponIcon(WeaponType type) => type switch
    {
        WeaponType.Rifle => rifle != null ? rifle : rifle = CreateSprite(DrawMask(RifleShape)),
        WeaponType.Pistol => pistol != null ? pistol : pistol = CreateSprite(DrawMask(PistolShape)),
        _ => shotgun != null ? shotgun : shotgun = CreateSprite(DrawMask(ShotgunShape)),
    };

    private static bool Box(float u, float v, float x0, float y0, float x1, float y1) => u >= x0 && u <= x1 && v >= y0 && v <= y1;

    private static bool LockShape(float u, float v)
    {
        if (Box(u, v, 0.18f, 0.08f, 0.82f, 0.55f)) return !(Box(u, v, 0.46f, 0.22f, 0.54f, 0.4f)); // 몸통 + 열쇠 구멍
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.55f));
        return v >= 0.55f && d <= 0.3f && d >= 0.18f;                                                    // 고리
    }

    private static bool HeartShape(float u, float v)
    {
        float x = (u - 0.5f) * 2.4f, y = (v - 0.45f) * 2.4f;
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y <= 0f;
    }

    private static bool ChestShape(float u, float v)
    {
        if (!Box(u, v, 0.08f, 0.15f, 0.92f, 0.8f)) return false;
        if (Box(u, v, 0.08f, 0.52f, 0.92f, 0.57f)) return false;            // 뚜껑 틈
        return !Box(u, v, 0.45f, 0.4f, 0.55f, 0.52f) || v < 0.42f;           // 자물쇠 자리
    }

    private static bool PortalShape(float u, float v)
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
        return (d <= 0.48f && d >= 0.38f) || (d <= 0.28f && d >= 0.2f) || d <= 0.08f;
    }

    private static bool CoinShape(float u, float v)
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
        return d <= 0.48f && !(d <= 0.32f && d >= 0.24f);
    }

    private static bool RifleShape(float u, float v)
        => Box(u, v, 0.3f, 0.5f, 0.97f, 0.58f)      // 총열
        || Box(u, v, 0.22f, 0.42f, 0.68f, 0.64f)    // 몸체
        || Box(u, v, 0.03f, 0.38f, 0.24f, 0.6f)     // 개머리
        || Box(u, v, 0.4f, 0.22f, 0.48f, 0.44f)     // 손잡이
        || Box(u, v, 0.54f, 0.2f, 0.63f, 0.44f);    // 탄창

    private static bool PistolShape(float u, float v)
        => Box(u, v, 0.15f, 0.55f, 0.85f, 0.75f)    // 슬라이드
        || (v >= 0.15f && v <= 0.58f && u >= 0.2f + (0.58f - v) * 0.25f && u <= 0.4f + (0.58f - v) * 0.25f); // 기울어진 손잡이

    private static bool ShotgunShape(float u, float v)
        => Box(u, v, 0.28f, 0.58f, 0.97f, 0.66f)    // 위 총열
        || Box(u, v, 0.28f, 0.48f, 0.97f, 0.55f)    // 아래 총열
        || Box(u, v, 0.58f, 0.38f, 0.78f, 0.48f)    // 펌프
        || Box(u, v, 0.03f, 0.36f, 0.3f, 0.64f)     // 개머리
        || Box(u, v, 0.3f, 0.26f, 0.38f, 0.48f);    // 손잡이

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

    // 정규화 좌표(0~1) 판정 함수로 실루엣 그리기. 2x2 슈퍼샘플로 가장자리 부드럽게
    private static Texture2D DrawMask(System.Func<float, float, bool> inside)
    {
        Texture2D texture = NewTexture();
        var pixels = new Color32[Resolution * Resolution];
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                        if (inside((x + 0.25f + sx * 0.5f) / Resolution, (y + 0.25f + sy * 0.5f) / Resolution)) hits++;
                pixels[y * Resolution + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
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
