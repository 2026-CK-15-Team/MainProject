# 던전 생성기 사용법

> AI로 작성한 문서임. 틀린 부분 있으면 바로 수정 바람.

시드 기반 자동 던전 생성기 쓰는 법 정리. 방 만드는 규칙은 [RoomTemplateGuide.md](RoomTemplateGuide.md) 참고.

---

## 1. 뭘 해주나

- 미리 만든 방 프리팹을 골라서 배치하고 복도로 이어줌.
- **같은 시드 = 항상 같은 맵.**
- 층 규칙
  - 전투방은 설정한 수만큼 딱 맞춰 생성
  - 특수방(시작·보물·상점·도박·비밀·보스·계단)은 전부 문 1개짜리 막다른 방
  - 보스방 뒤엔 계단방 (보스방만 문 2개)
  - 순환 켠 층은 전투방끼리 순환 딱 1개, 끈 층은 순환 없음
  - 상점·보물은 층마다 1개
  - 도박방은 런 전체 1개(35% 확률로 2개), 비밀방은 런 전체 1개(30% 확률로 2개). 어느 층에 나올진 랜덤.

## 2. 설정 에셋 만들기

Project 창 우클릭 → `Create > Dungeon`

### Floor Config (층마다 1개)
| 항목 | 설명 |
|---|---|
| Combat Room Count | 전투방 수 |
| Has Loop | 순환 1개 만들지 여부. 켜면 전투방 4개 이상 필요 |
| Has Stairs | 보스방 뒤 계단방 여부. 마지막 층은 끄면 됨 |
| Room Prefabs | 이 층에서 쓸 방 프리팹들. 나오는 방 종류마다 최소 1개 |
| Min Room Spacing | 방 사이 최소 간격(칸). 복도 꺾을 공간(문 폭 + 벽 두께)보다 작으면 알아서 늘어남 |
| Corridor Floor Tile | 복도 바닥 타일 |
| Top / Bottom / Side Wall Tile | 위·아래·좌우 벽 타일. 복도 벽 + 안 쓰는 문 막을 때 씀 |
| Bottom Wall Front Tile | 아래 벽이 바닥 위로 겹쳐 캐릭터 가리는 칸 타일 |

추천 세팅: 1층 순환 끔 / 2·3층 순환 켬 / 3층 계단 끔

### Run Config (1개)
| 항목 | 설명 |
|---|---|
| Floors | Floor Config를 1층부터 순서대로 |
| Double Gamble Chance | 도박방 2개 나올 확률 (기본 0.35) |
| Double Secret Chance | 비밀방 2개 나올 확률 (기본 0.3) |
| Wall Style | 벽 두께 (기본 위 3 / 아래 2 / 좌우 1). 방 그릴 때도 이 규칙대로. 자세한 건 가이드 2장 |

## 3. 미리보기 (방 없어도 됨)

1. 빈 오브젝트 만들고 `DungeonPreview` 붙이기
2. Run Config 연결, Run Seed / Floor Index 입력 (Floor Index는 0부터 = 1층)
3. 씬 뷰에서 확인
   - **View = Graph**: 방 연결 구조만 (색 = 방 종류, 선 = 문 연결)
   - **View = Placement**: 실제 배치. 방 테두리, 복도(연회색), 복도 벽(위 진함 · 아래 중간 · 좌우 연함, 숨는 칸 반투명), 막힌 문(빨강)
4. 층에 방 프리팹 없으면 임시 모양 방으로 그려줌 → 방 만들기 전에도 구조 확인 가능

컴포넌트 우클릭 메뉴
- `Randomize Seed`: 시드 랜덤으로 바꾸기
- `Validate 1000 Seeds`: 시드 1000개 돌려서 규칙 위반·복도 겹침·재현성 검사. 콘솔에 결과 뜸

## 4. 방 프리팹 준비

1. 가이드대로 방 만들기 (Grid + `Room` 컴포넌트 + Floor/Wall/WallFront 타일맵 + `RoomDoor`)
2. `Room` 인스펙터에서 Type 지정 → **Bake** 클릭
3. Floor Config의 Room Prefabs에 추가

방 수정하면 Bake 다시 누르기. 안 누르면 예전 문 위치로 배치됨.

## 5. 씬에서 실제 생성

### 씬 세팅
```
Dungeon (DungeonBuilder)
 ├─ Grid (Grid)
 │   ├─ CorridorFloor (Tilemap)
 │   ├─ CorridorWall      (Tilemap + TilemapCollider2D, Tag·Layer = Wall)
 │   └─ CorridorWallFront (Tilemap, 콜라이더 없음, 캐릭터보다 위에 그림)
 └─ Rooms (빈 오브젝트, 방 생성 위치)
```
`DungeonBuilder`에 Run Config, Grid, 복도 타일맵 3개, Rooms 연결.
복도 타일맵은 방 타일맵이랑 콜리전·정렬 설정 똑같이 (가이드 5장 참고).

### 테스트
`DungeonBuilder`의 Test Run Seed / Test Floor Index 입력 → 컴포넌트 우클릭 → `Build Test Floor`
지우려면 우클릭 → `Clear`

### 코드에서 쓰기
```csharp
dungeonBuilder.Build(runSeed, floorIndex);   // 층 생성 (이전 층은 자동으로 지움)
Room start = dungeonBuilder.StartRoom;       // 시작방 → 플레이어 배치할 때
foreach (Room room in dungeonBuilder.Rooms)  // 생성된 방 전부
{
    RoomType type = room.Node.Type;          // 방 종류
    // room.GetComponentsInChildren<RoomDoor>() → SetLocked(true/false)로 문 잠금
}
```
런 시작할 때 시드 하나 정해두고 층 넘어갈 때마다 같은 시드 + 다음 floorIndex로 `Build` 호출하면 됨.

## 6. 에러 날 때

| 메시지 | 원인 / 해결 |
|---|---|
| `템플릿 없는 방 종류 - ...` | 그 종류 방 프리팹이 Room Prefabs에 없음 → 추가 |
| `... 방에 맞는 템플릿 없음. 필요한 문: ...` | 필요한 방향에 문 있는 방이 없음 → 4면 다 문 있는 방 추가 |
| `순환 있는 층은 전투방 4개 이상 필요` | Has Loop 켠 층의 전투방 수 늘리기 |
| `N층 생성 실패` | 조건에 맞는 배치를 못 찾음. 대부분 문 방향 부족한 템플릿 문제 |
| Bake 경고 | 가이드 체크리스트 확인 |

## 7. 구조 (코드 볼 사람용)

```
RunPlanner      런 시드 → 층별 계획 (층 시드, 도박·비밀방 배정)
DungeonGenerator 층 계획 → 방 연결 구조 (격자 위 그래프)
TemplateSelector 방마다 맞는 템플릿 배정
LayoutSolver    격자 → 실제 타일 좌표, Z자 복도
FloorGenerator  위 과정 묶어주는 입구
DungeonBuilder  결과를 씬에 생성
LayoutValidator 규칙 검사
```
생성 로직(Generation 폴더)은 유니티 오브젝트 안 씀 → 순수 C#이라 결과가 항상 같음.
