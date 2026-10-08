using System.Collections.Generic;
using UnityEngine;

// 층 하나를 게임으로 만듦: 생성기(DungeonBuilder) 결과에 룸 런타임/콘텐츠를 붙이고,
// 플레이어 위치로 RoomEntered / CorridorEntered를 판정하고, 가림 덮개와 미니맵 데이터를 관리함.
[DefaultExecutionOrder(-200)]
public class FloorDirector : MonoBehaviour
{
    [SerializeField] private DungeonBuilder builder;

    private readonly List<RoomRuntime> rooms = new List<RoomRuntime>();
    private readonly Dictionary<Vector2Int, int> corridorByCell = new Dictionary<Vector2Int, int>();
    private readonly HashSet<int> discoveredCorridors = new HashSet<int>();
    private FogOfWar fog;
    private bool inCorridor;

    public DungeonBuilder Builder => builder;
    public IReadOnlyList<RoomRuntime> Rooms => rooms;
    public RoomRuntime CurrentRoom { get; private set; }     // 마지막으로 있던 룸 (미니맵 현재 룸 색)
    public bool InCorridor => inCorridor;
    public RoomRuntime StartRoom { get; private set; }
    public int FloorIndex { get; private set; } = -1;
    public bool IsBuilt => rooms.Count > 0;
    public bool TrackingEnabled { get; set; } = true;
    public IReadOnlyCollection<int> DiscoveredCorridors => discoveredCorridors;

    private void Awake()
    {
        GameServices.Floor = this;
        if (builder == null) builder = FindFirstObjectByType<DungeonBuilder>();
        fog = gameObject.AddComponent<FogOfWar>();
    }

    public void SetBuilder(DungeonBuilder value) => builder = value;

    // 층 생성. 실패하면 false (에러 로그)
    public bool BuildFloor(int runSeed, int floorIndex, RunPlan plan)
    {
        ClearFloor();
        FloorIndex = floorIndex;
        try
        {
            builder.Build(runSeed, floorIndex);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"층 생성 실패 ({floorIndex + 1}층, 시드 {runSeed}): {e.Message}\n{e.StackTrace}");
            return false;
        }

        GameConfig config = GameServices.Config;
        WallStyle style = builder.RunConfig.WallStyle;
        int floorCount = builder.RunConfig.Floors.Length;
        bool lastFloor = floorIndex >= floorCount - 1;
        FloorContent content = config != null ? config.GetFloor(floorIndex) : null;

        var byNode = new Dictionary<int, RoomRuntime>();
        foreach (Room room in builder.Rooms)
        {
            RoomRole role = MapRole(room.Node.Type, lastFloor);
            RoomRuntime runtime = room.gameObject.AddComponent<RoomRuntime>();
            runtime.Initialize(room, role, floorIndex, style);
            runtime.SetContent(CreateContent(runtime, role, floorIndex, plan, content));
            rooms.Add(runtime);
            byNode[room.Node.Id] = runtime;
            if (role == RoomRole.Start) StartRoom = runtime;
        }

        foreach (RoomRuntime runtime in rooms)
            foreach (RoomNode neighbor in runtime.Node.Neighbors)
                runtime.Neighbors.Add(byNode[neighbor.Id]);

        // 비밀 벽
        foreach (DungeonBuilder.SecretLink link in builder.SecretLinks)
        {
            RoomRuntime parent = byNode[link.Parent.Node.Id];
            RoomRuntime secret = byNode[link.Secret.Node.Id];
            secret.SecretParent = parent;
            SecretWall.Create(parent, secret, link.Parent, link.SealedCells, builder.CurrentFloor.CorridorFloorTile, link.ParentDoor);
        }

        RunPlan.AssignCombatPresets(floorIndex, rooms, content);

        // 복도 칸 색인 (미니맵, 복도 발견)
        FloorBlueprint blueprint = builder.Blueprint;
        for (int i = 0; i < blueprint.Corridors.Count; i++)
            foreach (Vector2Int cell in blueprint.Corridors[i].Cells)
                corridorByCell[cell] = i;

        fog.Build(builder.Grid, builder, rooms, content != null ? content.FogColor : new Color(0.16f, 0.13f, 0.11f), style);

        if (StartRoom == null) StartRoom = rooms[0];
        // StartRoom은 층 시작부터 방문 처리 (03 §6.4)
        StartRoom.MarkVisited();
        fog.RevealRoom(StartRoom, true);
        CurrentRoom = StartRoom;
        inCorridor = false;

        GameEvents.Raise(GameEvents.FloorGenerated, floorIndex);
        // 다음 층 배치를 미리 계산 (03 §6.2). 층 전환 검은 화면에서는 오브젝트만 만듦
        if (!lastFloor) builder.Precompute(runSeed, floorIndex + 1);
        return true;
    }

    // 층 전환 직전: 다음 층 미리 계산이 끝났는지 (03 §6.3 2번 "끝날 때까지 기다린다")
    public bool IsFloorReady(int runSeed, int floorIndex) => builder == null || builder.IsPrecomputeReady(runSeed, floorIndex);

    public static RoomRole MapRole(RoomType type, bool lastFloor) => type switch
    {
        RoomType.Start => RoomRole.Start,
        RoomType.Combat => RoomRole.Combat,
        RoomType.Treasure => RoomRole.Treasure,
        RoomType.Shop => RoomRole.Shop,
        RoomType.Gamble => RoomRole.Gamble,
        RoomType.Secret => RoomRole.Secret,
        RoomType.Boss => lastFloor ? RoomRole.FinalBoss : RoomRole.EliteGate,
        RoomType.Stairs => lastFloor ? RoomRole.RunEndTreasure : RoomRole.FloorExit,
        _ => RoomRole.Combat,
    };

    private RoomContent CreateContent(RoomRuntime runtime, RoomRole role, int floor, RunPlan plan, FloorContent content)
    {
        GameObject go = runtime.gameObject;
        switch (role)
        {
            case RoomRole.Combat:
                return go.AddComponent<CombatRoomController>();
            case RoomRole.EliteGate:
            {
                var combat = go.AddComponent<CombatRoomController>();
                combat.IsEliteGate = true;
                MonsterWavePreset preset = plan != null && GameServices.Config != null ? plan.ElitePreset(GameServices.Config, floor) : null;
                if (preset == null)
                {
                    preset = RunPlan.CreateFallbackPreset(floor, content, runtime);
                    preset.Waves.Add(preset.Waves[0]); // 개발용 2웨이브
                }
                combat.Preset = preset;
                return combat;
            }
            case RoomRole.Treasure: return go.AddComponent<TreasureRoomController>();
            case RoomRole.Shop: return go.AddComponent<ShopRoomController>();
            case RoomRole.Gamble:
                if (GameBalance.Current.SecretRoom.SecondUIFallback) return go.AddComponent<StartRoomController>();
                return go.AddComponent<GambleRoomController>();
            case RoomRole.Secret:
            {
                var secret = go.AddComponent<SecretRoomController>();
                secret.ContentKind = plan != null ? plan.SecretContentFor(floor, runtime.Node.Id) : SecretContent.Currency;
                return secret;
            }
            case RoomRole.FloorExit: return go.AddComponent<FloorExitController>();
            case RoomRole.FinalBoss: return go.AddComponent<FinalBossRoomController>();
            case RoomRole.RunEndTreasure: return go.AddComponent<RunEndTreasureController>();
            default: return go.AddComponent<StartRoomController>();
        }
    }

    public void ClearFloor()
    {
        Projectile.ClearAll();
        EnemyProjectile.ClearAll();
        foreach (Monster m in new List<Monster>(Monster.Alive)) if (m != null) m.Despawn();
        rooms.Clear();
        corridorByCell.Clear();
        discoveredCorridors.Clear();
        openedSecrets.Clear();
        CurrentRoom = null;
        StartRoom = null;
        inCorridor = false;
        fog.Clear();
        if (builder != null) builder.Clear();
    }

    private void Update()
    {
        if (!TrackingEnabled || !IsBuilt || GameClock.IsPaused) return;
        PlayerController player = GameServices.Player;
        if (player == null) return;
        Vector2 pos = player.Position;
        float r = player.BodyRadius;

        if (!inCorridor && CurrentRoom != null)
        {
            if (CurrentRoom.IsOutsideForCorridor(pos, r))
            {
                inCorridor = true;
                GameEvents.Raise(GameEvents.CorridorEntered);
                GameServices.Camera?.EnterCorridor();
            }
        }
        else
        {
            foreach (RoomRuntime room in rooms)
            {
                if (!room.ContainsForEntry(pos, r)) continue;
                EnterRoom(room, viaTeleport: false);
                break;
            }
        }

        if (inCorridor)
        {
            Vector2Int cell = Vector2Int.FloorToInt(pos);
            if (corridorByCell.TryGetValue(cell, out int index))
                discoveredCorridors.Add(index);
        }
    }

    private void EnterRoom(RoomRuntime room, bool viaTeleport)
    {
        bool first = !room.Visited;
        CurrentRoom = room;
        inCorridor = false;
        if (first)
        {
            room.MarkVisited();
            // 가림 해제: 전투룸 즉시, 비전투 룸 페이드
            fog.RevealRoom(room, room.IsCombatRoom);
        }
        GameServices.Camera?.EnterRoom(room, catchUp: room.IsCombatRoom && first && !viaTeleport);
        GameEvents.Raise(GameEvents.RoomEntered, room);
        room.OnEntered(first);
    }

    // 순간이동 도착 (08 §5.1)
    public void TeleportInto(RoomRuntime room)
    {
        EnterRoom(room, viaTeleport: true);
    }

    private readonly HashSet<RoomRuntime> openedSecrets = new HashSet<RoomRuntime>();

    public void OnSecretWallBroken(SecretWall wall, RoomRuntime secretRoom)
    {
        openedSecrets.Add(secretRoom);
        fog.OnSecretOpened(secretRoom);
    }

    public bool IsSecretOpen(RoomRuntime secretRoom) => openedSecrets.Contains(secretRoom);

    public PlacedRoom PlacedOf(RoomRuntime room)
        => builder != null && builder.Blueprint != null && room != null ? builder.Blueprint.Rooms[room.Node.Id] : null;

    public bool IsCorridorDiscovered(int index) => discoveredCorridors.Contains(index);
}
