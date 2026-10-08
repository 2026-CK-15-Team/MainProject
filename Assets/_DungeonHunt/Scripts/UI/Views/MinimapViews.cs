using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 08 §4.1 미니맵 그림. 칸 1개 = 텍스처 1픽셀. 타일 그림 대신 채움 색과 출구 표시만.
// 현재 룸 / 방문한 룸 / 복도 3색, 미방문 룸은 그리지 않음, 출구는 방문 룸의 쓰는 문 자리.
public static class MinimapRenderer
{
    public static readonly Color CurrentRoom = new Color(0.95f, 0.85f, 0.45f, 0.95f);
    public static readonly Color VisitedRoom = new Color(0.55f, 0.6f, 0.7f, 0.9f);
    public static readonly Color Corridor = new Color(0.4f, 0.42f, 0.48f, 0.85f);
    public static readonly Color Exit = new Color(1f, 1f, 1f, 1f);
    public static readonly Color Hover = new Color(0.6f, 1f, 0.7f, 0.95f);

    public static Texture2D Texture { get; private set; }
    public static RectInt Bounds { get; private set; }
    private static int lastSignature = -1;

    public static Color RoleColor(RoomRole role) => role switch
    {
        RoomRole.Start => Color.white,
        RoomRole.Shop => new Color(0.3f, 0.9f, 0.4f),
        RoomRole.Treasure => new Color(1f, 0.8f, 0.2f),
        RoomRole.Gamble => new Color(0.8f, 0.4f, 0.9f),
        RoomRole.Secret => new Color(0.6f, 0.6f, 0.6f),
        RoomRole.EliteGate => new Color(1f, 0.4f, 0.3f),
        RoomRole.FinalBoss => new Color(0.9f, 0.1f, 0.2f),
        RoomRole.FloorExit => new Color(0.4f, 0.85f, 1f),
        RoomRole.RunEndTreasure => new Color(1f, 0.95f, 0.5f),
        _ => new Color(0, 0, 0, 0),
    };

    public static string RoleIcon(RoomRole role) => role switch
    {
        RoomRole.Start => "S",
        RoomRole.Shop => "$",
        RoomRole.Treasure => "T",
        RoomRole.Gamble => "G",
        RoomRole.Secret => "?",
        RoomRole.EliteGate => "E",
        RoomRole.FinalBoss => "B",
        RoomRole.FloorExit => ">",
        RoomRole.RunEndTreasure => "★",
        _ => "",
    };

    // 방문/현재 룸/복도 발견이 바뀌었을 때만 다시 그림
    public static void Refresh(RoomRuntime hover = null)
    {
        FloorDirector floor = GameServices.Floor;
        if (floor == null || !floor.IsBuilt) return;
        int signature = ComputeSignature(floor, hover);
        if (signature == lastSignature && Texture != null) return;
        lastSignature = signature;

        RectInt bounds = ComputeBounds(floor);
        if (Texture == null || Texture.width != bounds.width || Texture.height != bounds.height)
        {
            Texture = new Texture2D(Mathf.Max(1, bounds.width), Mathf.Max(1, bounds.height), TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        Bounds = bounds;
        var pixels = new Color32[Texture.width * Texture.height];

        // 복도 (CorridorEntered 뒤 전체)
        FloorBlueprint blueprint = floor.Builder.Blueprint;
        for (int i = 0; i < blueprint.Corridors.Count; i++)
        {
            if (!floor.IsCorridorDiscovered(i)) continue;
            foreach (Vector2Int c in blueprint.Corridors[i].Cells) Set(pixels, c, Corridor);
        }

        foreach (RoomRuntime room in floor.Rooms)
        {
            if (!room.Visited) continue;
            Color fill = room == floor.CurrentRoom ? CurrentRoom : room == hover ? Hover : VisitedRoom;
            RoomNavGrid nav = room.Nav;
            foreach (Vector2Int cell in nav.WalkableCells())
                Set(pixels, room.CellBounds.position + cell, fill);

            // 출구 표시: 쓰는 문 자리 (파괴 전 비밀 벽 제외)
            PlacedRoom placed = floor.PlacedOf(room);
            if (placed != null)
            {
                foreach (RoomNode neighbor in room.Node.Neighbors)
                {
                    RoomRuntime other = floor.Rooms[neighbor.Id];
                    if (other.Role == RoomRole.Secret && !floor.IsSecretOpen(other)) continue;
                    if (room.Role == RoomRole.Secret && !floor.IsSecretOpen(room)) continue;
                    RoomSide side = room.Node.SideTowards(neighbor);
                    foreach (WallCell w in placed.GetOpeningSealCells(side, floor.Builder.RunConfig.WallStyle))
                        Set(pixels, placed.Position + w.Cell, Exit);
                }
            }

            // 종류 아이콘: 외접 사각형 중심 (깎인 칸이면 가까운 바닥 칸)
            Color icon = RoleColor(room.Role);
            if (icon.a > 0f)
            {
                Vector2Int center = Vector2Int.FloorToInt(room.SafeCenter());
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        Set(pixels, center + new Vector2Int(dx, dy), icon);
            }
        }

        Texture.SetPixels32(pixels);
        Texture.Apply();
    }

    private static void Set(Color32[] pixels, Vector2Int worldCell, Color color)
    {
        int x = worldCell.x - Bounds.x, y = worldCell.y - Bounds.y;
        if (x < 0 || y < 0 || x >= Texture.width || y >= Texture.height) return;
        pixels[y * Texture.width + x] = color;
    }

    private static RectInt ComputeBounds(FloorDirector floor)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        foreach (RoomRuntime room in floor.Rooms)
        {
            xMin = Mathf.Min(xMin, room.CellBounds.xMin); yMin = Mathf.Min(yMin, room.CellBounds.yMin);
            xMax = Mathf.Max(xMax, room.CellBounds.xMax); yMax = Mathf.Max(yMax, room.CellBounds.yMax);
        }
        foreach (Vector2Int c in floor.Builder.Blueprint.CorridorFloor)
        {
            xMin = Mathf.Min(xMin, c.x); yMin = Mathf.Min(yMin, c.y);
            xMax = Mathf.Max(xMax, c.x + 1); yMax = Mathf.Max(yMax, c.y + 1);
        }
        return new RectInt(xMin - 2, yMin - 2, xMax - xMin + 4, yMax - yMin + 4);
    }

    private static int ComputeSignature(FloorDirector floor, RoomRuntime hover)
    {
        unchecked
        {
            int hash = floor.FloorIndex * 7919 + floor.Rooms.Count;
            foreach (RoomRuntime room in floor.Rooms) hash = hash * 31 + (room.Visited ? 1 : 0) + (floor.IsSecretOpen(room) ? 2 : 0);
            hash = hash * 31 + floor.DiscoveredCorridors.Count;
            hash = hash * 31 + (floor.CurrentRoom != null ? floor.CurrentRoom.Node.Id + 1 : 0);
            hash = hash * 31 + (hover != null ? hover.Node.Id + 1 : 0);
            hash = hash * 31 + floor.GetInstanceID();
            return hash;
        }
    }

    public static void Invalidate() => lastSignature = -1;
}

// 상시 HUD 미니맵: 플레이어 중심, 1배 룸 하나와 출구가 들어오는 범위
public class MinimapWidget
{
    private RawImage image;
    private Image playerDot;
    private RectTransform root;

    public void Create(RectTransform parent)
    {
        Image frame = UIFactory.Panel("Minimap", parent, new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Place(frame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(300f, 300f));
        root = frame.rectTransform;
        image = UIFactory.Raw("Map", root);
        UIFactory.Stretch(image.rectTransform, 6f);
        playerDot = UIFactory.Panel("Player", root, new Color(1f, 0.3f, 0.3f));
        UIFactory.Place(playerDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
    }

    public void Tick()
    {
        MinimapRenderer.Refresh();
        Texture2D texture = MinimapRenderer.Texture;
        PlayerController player = GameServices.Player;
        if (texture == null || player == null) return;
        image.texture = texture;
        float range = GameBalance.Current.UI.MinimapHUDViewRange;
        RectInt bounds = MinimapRenderer.Bounds;
        Vector2 p = player.Position - (Vector2)bounds.position;
        image.uvRect = new Rect((p.x - range * 0.5f) / texture.width, (p.y - range * 0.5f) / texture.height, range / texture.width, range / texture.height);
    }
}

// 확대 미니맵 (08 §5.1): 층 전체, 현재 층 수, 방문 룸 남은 아이템, 방문 룸 클릭 → 순간이동
public class ExpandedMapView : UIView
{
    private RawImage image;
    private RectTransform mapRect;
    private RectTransform iconRoot;
    private Text header;
    private Image playerDot;
    private RoomRuntime hover;

    public override ModalKind Kind => ModalKind.NonStop;

    protected override void Build(RectTransform root)
    {
        RectTransform panel = CenterPanel(root, new Vector2(1600f, 960f));
        header = Title(panel, "");
        image = UIFactory.Raw("Map", panel);
        mapRect = image.rectTransform;
        UIFactory.Place(mapRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1500f, 820f));
        iconRoot = UIFactory.CreateRect("Icons", mapRect);
        UIFactory.Stretch(iconRoot);
        playerDot = UIFactory.Panel("Player", mapRect, new Color(1f, 0.3f, 0.3f));
        playerDot.rectTransform.sizeDelta = new Vector2(12f, 12f);
        Text help = UIFactory.Label("Help", panel, "방문한 룸을 클릭하면 순간이동 · Tab / ESC 닫기", 24, TextAnchor.LowerCenter, new Color(0.7f, 0.7f, 0.75f));
        UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1200f, 40f));
    }

    protected override void OnOpened()
    {
        hover = null;
        MinimapRenderer.Invalidate();
        Rebuild();
    }

    private void Rebuild()
    {
        MinimapRenderer.Refresh(hover);
        Texture2D texture = MinimapRenderer.Texture;
        if (texture == null) return;
        image.texture = texture;
        image.uvRect = new Rect(0, 0, 1, 1);
        // 층 전체가 한 화면에 들어오게 비율 유지
        float scale = Mathf.Min(1500f / texture.width, 820f / texture.height);
        mapRect.sizeDelta = new Vector2(texture.width * scale, texture.height * scale);
        header.text = $"{GameServices.Floor.FloorIndex + 1}층";

        UIFactory.Clear(iconRoot);
        foreach (RoomRuntime room in GameServices.Floor.Rooms)
        {
            if (!room.Visited) continue;
            string icon = MinimapRenderer.RoleIcon(room.Role) + RemainingItems(room);
            if (string.IsNullOrEmpty(icon)) continue;
            Text label = UIFactory.Label("Icon", iconRoot, icon, 22, TextAnchor.MiddleCenter, Color.black);
            Vector2 pos = CellToLocal(room.SafeCenter() + new Vector2(0f, 1.6f), scale);
            UIFactory.Place(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), pos, new Vector2(160f, 30f));
        }
    }

    // 남은 아이템 종류 아이콘 (아티팩트, 재화, HP). 개수 표시 없음
    private static string RemainingItems(RoomRuntime room)
    {
        bool artifact = false, currency = false, heal = false;
        foreach (ArtifactPickup a in ArtifactPickup.Active) if (a != null && a.transform.IsChildOf(room.transform)) artifact = true;
        foreach (RewardPickup r in RewardPickup.Active)
        {
            if (r == null || !r.transform.IsChildOf(room.transform)) continue;
            if (r.Kind == RewardKind.Currency) currency = true; else heal = true;
        }
        string s = "";
        if (artifact) s += " ◆";
        if (currency) s += " ●";
        if (heal) s += " ♥";
        return s;
    }

    private Vector2 CellToLocal(Vector2 world, float scale)
    {
        RectInt b = MinimapRenderer.Bounds;
        return new Vector2((world.x - b.x) * scale, (world.y - b.y) * scale);
    }

    public override void Tick()
    {
        Texture2D texture = MinimapRenderer.Texture;
        if (texture == null) return;
        float scale = mapRect.sizeDelta.x / texture.width;

        PlayerController player = GameServices.Player;
        if (player != null)
        {
            playerDot.rectTransform.anchorMin = playerDot.rectTransform.anchorMax = Vector2.zero;
            playerDot.rectTransform.anchoredPosition = CellToLocal(player.Position, scale);
        }

        // 클릭 범위 = 방문 룸의 외접 사각형. 마우스를 올리면 실제 바닥 모양 강조
        RoomRuntime hit = null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, InputGate.MousePosition, null, out Vector2 local))
        {
            Vector2 fromCorner = local + mapRect.sizeDelta * 0.5f;
            Vector2 world = fromCorner / scale + (Vector2)MinimapRenderer.Bounds.position;
            Vector2Int cell = Vector2Int.FloorToInt(world);
            foreach (RoomRuntime room in GameServices.Floor.Rooms)
                if (room.Visited && room.ContainsCell(cell)) { hit = room; break; }
        }
        if (hit != hover)
        {
            hover = hit;
            MinimapRenderer.Refresh(hover);
        }
        if (hover != null && InputGate.LeftMouseDown && GameServices.Run.CanTeleport(hover))
        {
            RoomRuntime target = hover;
            Manager.CloseView(this);
            GameServices.Run.TeleportTo(target);
        }
    }
}
