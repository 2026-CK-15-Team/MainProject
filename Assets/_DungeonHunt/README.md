에디터 버전6000.3.23f1
폴더경로
_DungeonHunt
    ㄴArt 아티스트 작업물은 이안에 넣어주세요
    ㄴInput
    ㄴPrefabs
    ㄴScenes
    ㄴScripts
    ㄴSettings
    ㄴThird party 외부에셋설치시 이안에 넣어주세요

2026-09-29

## 플레이테스트 로그 확인법

경로 두 곳
- 에디터 전용 사본, Project 창에서 바로 보임: Assets/PlaytestLogs/
- 실제 저장 위치: %userprofile%\AppData\LocalLow\{Company Name}\{Product Name}\PlaytestLogs\
- 파일명은 {RunID}.json, 런마다 새로 생김, 가장 최근 수정 파일이 마지막 런
- 룸 클리어 시점(RoomCleared)마다 파일에 쓰기 때문에, 클리어하기 전까진 메모리에만 있고 파일엔 안 보임
- 콘솔(Console)에는 안 뜸. Debug.Log와는 별도 경로임

## 로그 목록
관련 항목 사항은 프로토타입_AC검증 에서확인
| 로그 이름 | 클래스 위치 | 관련 항목 |
|---|---|---|
| ArtifactDraw | ArtifactDrawer | 7 |
| Combat2Draw | ArtifactDrawer | 7, 13, 17 |
| ArtifactShown | ArtifactAcquireUI | 17 |
| ArtifactEquip | PlayerArtifacts | 7 |
| ArtifactReroll | FieldArtifact | 13, 17 |
| ArtifactRerollFailed | FieldArtifact | 13 |
| Tier1 | PlayerArtifacts | 9번대 검증(Tier1 활성/해제) |
| RifleStackHit | RifleStackTracker | 9, 10, 17 |
| Fire | WeaponController, ConeFireMode | 17 |
| ReloadStart | WeaponRuntime | 17 |
| ReloadComplete | WeaponRuntime | 17 |
| ReloadCancelled | WeaponController | 8번대 검증(재장전 취소) |
| WeaponSwap | WeaponController | 17 (무기 장착 시간) |
| PistolDeflect | Projectile | 17 (권총 상쇄) |
| MonsterHit | DummyEnemy | 17 (명중과 최종 피해) |
| PlayerHit | PlayerHealth | 검증용 |
| Heal | PlayerHealth | 17 |
| Death | PlayerHealth | 18 |
| Dodge | PlayerMovement | 검증용 |
| JustDodge | PlayerMovement | 검증용 |
| CurrencyGain | RunCurrency | 17 |
| CurrencySpend | RunCurrency | 17 |
| ShopPurchase | ShopSlot | 검증용 |
| RoomEntered | RoomEntryTrigger | 검증용 |
| RoomCleared | RoomController, FinalCombatController | 검증용 |
| Wave1Cleared | FinalCombatController | 4 |
| Wave2TelegraphStart | FinalCombatController | 4 |
| Wave2Spawned | FinalCombatController | 4 (elapsed 값이 실측 경과시간) |
| SafeZoneCheck | EntrySafeZoneValidator | 5 |

## 참고
- Combat2Draw는 최초 보상과 리롤 양쪽에서 다 남음. source 값으로 구분: combat2, combat2-reroll
- SafeZoneCheck는 EntrySafeZoneValidator를 룸에 연결 안 하면 안 찍힘, 에러도 안 뜸
- 콘솔에서만 보이는 것(PlaytestLogger 안 거침): [RifleStack], [SwapEnhance], [HitStopState], [Artifact]


## Prefabs 폴더 안내

### Artifacts
Tier1 세트 아티팩트 프리팹. 씬에 배치하면 그대로 습득 가능.

### Item
- Shop Slot 기반: Artifacts, Heal_Item
- Field Artifact 기반: Treasure

### EnemyPrefabs
정지 허수아비, 각 종 몬스터, 적용 투사체 프리팹 있음.
인스펙터에서 능력치 수정 가능하나, 테스트용으로 건드릴 땐 프리팹 원본이 아니라 씬에 생성된 쪽에서만 수정할 것.

로그 저장 실패 시 비차단 테스트 방법
진짜 저장 경로 찾기: 시작시 콘솔 천번째 항목에 경로가 표시됨 해당 콘솔에 찍힌 경로 복사 (에디터 사본인 Assets/PlaytestLogs가 아니라 이 경로를 써야 함, 실제 쓰기는 이쪽에서 먼저 실패해야 재현됨)
그 경로의 PlaytestLogs 폴더에서 룸 하나 클리어해 {RunID}.json 생성
그 json 파일 우클릭 → 속성 → 읽기 전용 체크
같은 런으로 돌아가 룸을 하나 더 클리어하거나 사망/포기로 런 종료

확인할 것
콘솔에 [PlaytestLogger] 저장 실패 경고 뜨는지
우측 상단에 로그 실패 토스트(PlaytestLogFailureToast) 표시되는지
그 이후에도 플레이, Restart, 로비 이동이 전부 정상 작동하는지 (막히면 안 됨)
테스트 끝나면 읽기 전용 체크 반드시 해제할 것 (안 하면 그 파일 계속 저장 실패 상태로 남음)

참고: 로그는 이벤트마다 즉시 저장되지 않음

PlaytestLogger는 룸 클리어(RoomCleared)나 런 종료 시점에만 파일을 씀. 그 사이 구간(Shop 체류 등)의 이벤트는 메모리에만 있다가 다음 저장 시점에 한꺼번에 기록됨. 중간에 플레이를 멈추고 json을 열면 최근 이벤트가 없는 것처럼 보일 수 있으나, 다음 저장 시점까지 진행하면 포함됨.