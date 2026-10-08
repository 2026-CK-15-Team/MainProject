using UnityEngine;
using UnityEngine.Tilemaps;

// 게임 씬 진입점. 빠진 매니저와 오브젝트를 만들어 연결함.
// 씬에는 이 컴포넌트(GameConfig 연결)만 있어도 첫 화면부터 동작함. 이미 씬에 있는 것은 그대로 씀.
[DefaultExecutionOrder(-2000)]
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameConfig config;

    public const int FloorSortingOrder = 0;
    public const int WallSortingOrder = 115;
    public const int WallFrontSortingOrder = 120;

    private void Awake()
    {
        if (config == null) config = Resources.Load<GameConfig>("GameConfig");
        if (config == null) Debug.LogError("GameBootstrap: GameConfig가 없음. 'DungeonHunt/Setup/Generate Placeholder Content' 메뉴로 만들거나 연결할 것");
        GameServices.Config = config;
        if (config != null && config.UIFont != null) PlaceholderArt.UIFont = config.UIFont;

        GetOrAdd<GameClockDriver>(gameObject);
        GetOrAdd<SaveService>(gameObject);
        GetOrAdd<RunWallet>(gameObject);
        GetOrAdd<ArtifactSystem>(gameObject);
        GetOrAdd<FeedbackService>(gameObject);

        EnsureCamera();
        DungeonBuilder builder = EnsureDungeon();
        FloorDirector floor = GetOrAdd<FloorDirector>(gameObject);
        floor.SetBuilder(builder);
        EnsurePlayer();

        GetOrAdd<UIManager>(gameObject);
        GetOrAdd<RunManager>(gameObject);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T existing = FindFirstObjectByType<T>();
        if (existing != null) return existing;
        return go.AddComponent<T>();
    }

    private void EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 0f, -10f);
        }
        cam.backgroundColor = new Color(0.06f, 0.05f, 0.05f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        if (cam.GetComponent<CameraController>() == null) cam.gameObject.AddComponent<CameraController>();
    }

    private DungeonBuilder EnsureDungeon()
    {
        DungeonBuilder builder = FindFirstObjectByType<DungeonBuilder>();
        if (builder != null && builder.IsConfigured) return builder;

        var root = builder != null ? builder.gameObject : new GameObject("Dungeon");
        if (builder == null) builder = root.AddComponent<DungeonBuilder>();
        var gridGo = new GameObject("Grid");
        gridGo.transform.SetParent(root.transform, false);
        Grid grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);
        Tilemap floorMap = CreateTilemap(gridGo.transform, "CorridorFloor", FloorSortingOrder, collider: false);
        Tilemap wallMap = CreateTilemap(gridGo.transform, "CorridorWall", WallSortingOrder, collider: true);
        Tilemap frontMap = CreateTilemap(gridGo.transform, "CorridorWallFront", WallFrontSortingOrder, collider: false);
        var rooms = new GameObject("Rooms");
        rooms.transform.SetParent(root.transform, false);
        builder.Configure(config != null ? config.Run : null, grid, floorMap, wallMap, frontMap, rooms.transform);
        return builder;
    }

    public static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Tilemap map = go.AddComponent<Tilemap>();
        var renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        if (collider)
        {
            int wallLayer = GameLayers.Wall;
            if (wallLayer >= 0) go.layer = wallLayer;
            try { go.tag = GameLayers.WallTag; } catch (UnityException) { Debug.LogWarning("Wall 태그 없음 (에디터 작업 목록 참고)"); }
            go.AddComponent<TilemapCollider2D>();
        }
        return map;
    }

    private void EnsurePlayer()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
        {
            PlayerMovement legacy = FindFirstObjectByType<PlayerMovement>(FindObjectsInactive.Include);
            if (legacy != null) player = legacy.gameObject.AddComponent<PlayerController>();
        }
        if (player == null && config != null && config.PlayerPrefab != null)
        {
            GameObject go = Instantiate(config.PlayerPrefab);
            go.name = "Player";
            player = go.GetComponent<PlayerController>() ?? go.AddComponent<PlayerController>();
        }
        if (player == null)
        {
            var go = new GameObject("Player");
            int playerLayer = LayerMask.NameToLayer(GameLayers.PlayerLayerName);
            if (playerLayer >= 0) go.layer = playerLayer;
            player = go.AddComponent<PlayerController>();
        }
        // 비활성으로 놓여 있던 플레이어도 Awake가 돌게 한 번 켬
        player.gameObject.SetActive(true);
        GameServices.Player = player;
        if (config != null && config.Weapons != null && config.Weapons.Length > 0 && !player.Weapons.HasDefinitions)
            player.Weapons.SetDefinitions(config.Weapons);
        player.gameObject.SetActive(false);
    }
}
