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

2026-9-29

## 플레이테스트 로그 확인법

경로 두 곳
- 에디터 전용 사본, Project 창에서 바로 보임: Assets/PlaytestLogs/
- 실제 저장 위치: %userprofile%\AppData\LocalLow\{Company Name}\{Product Name}\PlaytestLogs\
- 파일명은 {RunID}.json, 런마다 새로 생김, 가장 최근 수정 파일이 마지막 런
- 룸 클리어 시점(RoomCleared)마다 파일에 쓰기 때문에, 클리어하기 전까진 메모리에만 있고 파일엔 안 보임
- 콘솔(Console)에는 안 뜸. Debug.Log와는 별도 경로임

## 로그 목록
관련 항목사항은 프로토타입_AC검증 에서 확인
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