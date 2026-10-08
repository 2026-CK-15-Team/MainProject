# 시스템 구조 설명

> AI로 작성한 문서임. 코드 보는 사람용. 틀린 부분 있으면 바로 수정 바람.

계획/상태는 [GameSystemPlan.md](GameSystemPlan.md), 사용법은 [Manual.md](Manual.md).

---

## 1. 폴더 구조

```
Scripts/
 ├─ Core/          전투 시계, 이벤트 버스, 서비스 참조, 입력 게이트, 벽 이동/판정 수학, 기본 도형, 부트스트랩
 ├─ Config/        GameBalance(전 수치), GameConfig(루트 설정), FloorContent(층 콘텐츠), MonsterWavePreset
 ├─ Stats/         01-4 스탯 합성 (PlayerStats)
 ├─ Player/        01-1 이동/회피, 01-3 생존, 입력, 상호작용, 표현
 ├─ Weapons/       01-2 무기 런타임, 발사, 투사체, 샷건 원추
 ├─ Combat/        DamageInfo, 공격 대상 목록(CombatTargets)
 ├─ Enemies/       04 몬스터 데이터, 공통 FSM, 아키타입 5종, 적 투사체, 경로 탐색, 예고 표시
 ├─ Boss/          05 최종보스
 ├─ Artifacts/     07 아티팩트 데이터, 추첨/장착 시스템, 필드 아티팩트
 ├─ Economy/       런전용 재화 지갑
 ├─ Pickups/       필드 보상 (재화, HP)
 ├─ Rooms/         룸 런타임, 룸 종류별 컨트롤러, 층 감독, 가림 덮개, 비밀 벽
 ├─ Run/           06 런 흐름(RunManager), 런 계획, 영구 진행 규칙
 ├─ Save/          세이브 데이터, 저장 서비스
 ├─ CameraSystem/  02 카메라, 16:9 필러박스/레터박스
 ├─ Feedback/      09 연출 출력
 ├─ Audio/         임시 합성 사운드 (GameEvents 구독, 설정 음량)
 ├─ UI/            08 UI 매니저, 화면/팝업 (코드로 조립)
 ├─ Editor/        자리 표시 콘텐츠 생성기, 검증기, 디버그 창
 └─ Dungeon/       (기존) 던전 생성기 — 비밀 벽 봉인, Blueprint 노출, 다음 층 미리 계산, 도박방 폴백만 추가
```

---

## 2. 핵심 원칙

### 2.1 상태 소유자는 하나

기획서의 "한 시스템의 상태는 한 문서만 소유"를 코드에도 그대로 씀.

| 상태 | 소유 | 다른 시스템은 |
|---|---|---|
| 최종 스탯 | `PlayerStats` | 수정값을 **원천 이름** 단위로 등록/제거만 (`SetSource("Artifact:ID", mods)`) |
| HP, 무적, 사망 | `PlayerHealth` | `ReceiveAttack()`으로 공격만 넘김 |
| 탄창, 재장전 | `WeaponRuntime` | 읽기만 |
| 아티팩트 슬롯, 예약 | `ArtifactSystem` | `DrawAndReserve`, `TryAutoEquip`, `GrantImmediate` 호출 |
| 런 진행/정산 | `RunManager` | 이벤트로 알림 |

### 2.2 전투 시계 (`GameClock`)

게임플레이 타이머는 `Time.time` 대신 `GameClock.CombatTime / CombatDelta`를 씀.

| 상황 | 전투 시계 | Time.timeScale | 비고 |
|---|---|---|---|
| 일시정지 메뉴, 층 전환, 순간이동, 보스 등장, 저장 실패, 포커스 이탈, 사망 | 정지 | 0 | 정지 사유(PauseReason)별로 관리. 하나라도 있으면 정지 |
| 처치 히트스톱 | 정지 | 1 | 이펙트, 흔들림, UI, RunTime은 계속 (09 §6) |
| 비정지 모달 (상점, 미니맵, 아티팩트 UI 등) | 흐름 | 1 | 재장전도 계속 흐름 (08 §6) |

UI, 페이드, 연출은 `Time.unscaledDeltaTime` 사용.

### 2.3 판정은 수학, 벽만 물리

물리 엔진 콜백 순서에 결과가 흔들리지 않게 (01-3 §7.2 "렌더링, 콜백, 순회 순서는 결과를 바꾸지 않는다"):

- **벽**: Physics2D 쿼리 (Layer `Wall`). 타일맵 벽, 문 장벽(잠금 이펙트), 장애물
- **몬스터 피격**: `CombatTargets` 목록을 선분-원 스윕으로 검사 → 최초 충돌 하나, 동률이면 벽 > 몬스터, 몬스터끼리는 `SpawnOrder`
- **플레이어 피격**: 플레이어 피격원(`HurtboxRadius 0.18H`)과 수학 검사
- **이동**: `KinematicMover2D` — 원형 몸체 캐스트 후 벽 방향 성분 제거(미끄러짐). 플레이어와 몬스터는 서로 통과

### 2.4 처리 틱 순서 (01-1 §7.6)

`DefaultExecutionOrder`로 고정:

| 순서 | 컴포넌트 | 하는 일 |
|---|---|---|
| -2000 | GameBootstrap | (Awake만) 빠진 매니저 생성 |
| -1000 | GameClockDriver | 전투 시계 진행 |
| -450 | RunManager | 런 흐름 |
| -400 | PlayerController | |
| -350 | UIManager | 모달 입력 → ESC → F → Tab → I (UI 입력이 이번 틱을 소비하면 게임 입력 버림) |
| -300 | PlayerInputReader | 회피 → 무기 전환 → 공격 → 재장전 |
| -200 | FloorDirector | RoomEntered / CorridorEntered 판정 |
| 0 | 이동, 무기, 투사체, 몬스터 | 타이머 완료 → 행동/AI/이동 → 충돌 |
| 50 (LateUpdate) | PlayerHealth | 같은 틱 피해 합산 확정, 사망 |
| 100 (LateUpdate) | CombatRoomController, FinalBossRoomController | 클리어 판정 (플레이어 사망이 같은 틱이면 클리어 안 함) |

---

## 3. 시스템별 동작

### 3.1 런 흐름 (`RunManager`)

```
Boot ─ 세이브 로드(손상 → 초기화+안내) ─ 새 세이브 즉시 저장
  └→ 첫 화면 ─ 게임 시작 → 로비 ─ 탐사 시작 → InRun
                                     ├ 층 진행: FloorExitRoom F → 층 전환 (페이드, 1초 넘으면 대기 표시)
                                     ├ 사망 → 사망 효과+페이드 0.75s → 결과창(RunOver)
                                     ├ 런 포기 → 페이드 0.3s → 결과창(RunOver)
                                     └ 엔딩 상자 F → RunCleared 즉시 저장 → 페이드 → 엔딩 12s → 결과창
결과창 ─ 로비로 → 정산 ID로 1회 반영 → 저장 → 로비
```

- **런 시드**: 런마다 1개. `RandomStreams`가 전투/보상/콘텐츠 난수로 파생 (콘텐츠는 런 시드 + 층/방 번호로 결정 → 재현 가능)
- **저장 실패**: 1차 팝업(재시도) → 2차 팝업(첫 화면으로). 스냅샷은 메모리에 남고, 첫 화면 "게임 시작"은 저장 성공해야 로비로
- **영구 재화 예정량** = 전투룸 클리어 수 x 2 + 층 클리어 보너스(4/7/10) + 상점 영구재화 묶음

### 3.2 층 (`FloorDirector`)

1. `DungeonBuilder.Build(runSeed, floor)` — 기존 생성기로 방 배치(미리 계산한 결과가 있으면 그것 사용), 복도 칠하기, **비밀방과 이어진 부모 입구를 벽으로 봉인**
2. 방마다 `RoomRuntime` + 종류별 `RoomContent` 부착
3. 비밀 벽(`SecretWall`) 생성, 전투룸 몬스터 구성 배정(`RunPlan.AssignCombatPresets`), 가림 덮개(`FogOfWar`)
4. `FloorGenerated` 직후 다음 층 배치를 `Task.Run`으로 미리 계산 (순수 C# 생성기라 스레드 안전, Unity API는 메인에서만). 층 전환은 결과를 기다렸다가 오브젝트만 만듦
5. 매 프레임 플레이어 위치로 진입 판정: 바닥 경계에서 몸체 반경만큼 안쪽 = RoomEntered, 바깥 = CorridorEntered
6. 가림 덮개: 미방문 룸 외곽 전체 + 방문 룸과 안 이어진 복도. 방문 룸에 이어진 복도가 미방문 룸에 닿는 곳은 **문 틈**(바닥 경계 한 칸 반투명 검정)

### 3.3 전투룸 (`CombatRoomController`)

```
Unvisited ─ 첫 RoomEntered → 문 잠금 + 1웨이브 즉시 활성 (소환 이펙트 없음)
Locked ─ 웨이브 전멸 → (히트스톱 끝) → 예고 1초 → 다음 웨이브 (접촉 피해 유예 0.25s)
       └ 마지막 웨이브 전멸 + 플레이어 생존 → 잠금 해제 → "클리어" → 보상
Cleared ─ 재방문 자유
```
보상: 아티팩트 20% / 재화 55%(6) / HP 25%(1) → RewardSpawnPoint. EliteGate는 희귀 이상 아티팩트 1개 확정 + FloorExitAvailable.

### 3.4 몬스터 (`Monster` + `MonsterBehaviour`)

- **공격 예약**: 활성화 시각 t0에서 `D ~ U[max(I/2, T), I]` 뒤 첫 실행. 예고 시작 = 실행 - T. 다음 간격은 **실제 실행 시각**부터 (Armed 대기로 늦어져도 간격 안 줄어듦), 실행 직전 취소는 **취소 시각**부터
- **상태**: ActiveMovement → (조건 X) Armed → AttackTelegraph → DirectionLocked(실행 0.25s 전) → AttackExecute → (돌진형) PostDashRecovery
- **넉백**: 상태를 바꾸지 않는 이동 억제. `v(t) = 2S/Dk x (1 - t/Dk)`, 벽 닿으면 즉시 종료, 넉백 중 추가 넉백 무시, 돌진 중 면역
- **사망**: 미확정 예고 취소, 소유 투사체 즉시 제거(공통 소멸 표현), 처치 재화 즉시 지급, 히트스톱(같은 공격은 1회)

| 아키타입 | 시작 검사 | 실행 검사 | 실패 시 |
|---|---|---|---|
| 돌진형 | 거리 3H | 생존만 | 잠근 방향으로 돌진 |
| 근접 유지형 | 사거리+LOS | 꼭짓점→플레이어 LOS | 빗나감 |
| 조준 사격형 | 사거리+LOS (예고 없음) | 연발 둘째 발부터 검사 없음 | 끝까지 쏨, 셋째 발만 선행 조준 |
| 탄막 패턴형 | 사거리+LOS | 사거리+LOS | 취소 (차례 안 넘김) |
| 예고 후 저격형 | 사거리 | 사거리 | 취소, 벽 무시 즉발 선분 |

### 3.5 아티팩트 (`ArtifactSystem`)

추첨: 원본 경로 등급 가중치 → 유효 ID 0개 등급 제외 후 재정규화 → 등급 추첨 → (50%) 장착 중 미완성 세트 하나 균등 → ID 균등.
제외: 현재 장착 / 활성 예약(월드 복사본) / 같은 세션 확인 ID.

| 획득 경로 | 방식 | 리롤 |
|---|---|---|
| 보물룸, 전투룸, 비밀방, EliteGate | 필드 배치 → F → 획득 UI | 첫 세션만 2회 (1회 무료, 2회 6재화) |
| 상점, 도박 | 즉시 지급 | 없음 |

장착 불가(슬롯 가득/특화 충돌) → 획득 대기 모드 강제 개방. 무기 특화 세트는 Tier1부터 다른 두 무기 잠금 + 들고 있으면 특화 무기로 강제 전환.

### 3.6 UI (`UIManager`)

- 캔버스 3단: 화면(100) / 페이드(200) / 시스템 팝업(300). 저장 실패 팝업은 검은 화면 위에도 보임
- 모달 그룹: 시간 정지(일시정지/조작법/설정) · 비정지(미니맵/보유 아티팩트/상점/도박/획득) · 시스템(포커스/저장 실패/안내)
- ESC 순서 (08 §6.1): 시스템 팝업이면 무시 → 엔딩 구간 → 최상단 닫기 → 온보딩/결과/전환 중 무시 → 일시정지 토글
- 모든 화면은 `UIView.Build()`에서 코드로 조립. 아트 들어오면 Build만 바꾸거나 프리팹으로 교체
- 16:9: 메인 카메라 viewport를 `LetterboxCamera`가 맞추고, 각 캔버스 아래 `View16x9`(AspectRatioFitter)에 모든 화면을 둠. CanvasScaler 기준을 화면비에 따라 가로/세로로 바꿔 16:9 영역이 항상 1920x1080 단위

### 3.7 세이브

`Application.persistentDataPath/permanent.json` (영구 진행), `settings.json` (설정). 임시 파일에 쓰고 교체하는 방식.
저장 시점: 결과창 로비로 / 로비 구매 / 도감 새 발견 / 진행 해금 달성 / RunCleared 순간 / FloorReached(최고 층) / 첫 CorridorEntered(온보딩 완료).

---

## 4. 확장 포인트

| 하고 싶은 것 | 방법 |
|---|---|
| 새 스탯 | `StatId`에 추가 → `PlayerStats.BaseValue/Clamp` → 쓰는 쪽에서 `stats.Get()` |
| 아티팩트 특수 효과 | `GameEvents` 구독하는 컴포넌트를 만들고 장착 상태(`ArtifactSystem.Slots`)를 보고 동작. 스탯만이면 `Modifiers`로 충분 |
| 새 몬스터 아키타입 | `MonsterArchetype` 추가 → `MonsterBehaviour` 상속 클래스 → `MonsterBehaviour.Create`에 연결 |
| 보스 패턴 | `BossPatternKind` 추가 후 `FinalBoss.DirectionFor/Execute`, 또는 BossDefinition 패턴 목록 데이터로 |
| 새 룸 종류 | 생성기 `RoomType` → `FloorDirector.MapRole/CreateContent` → `RoomContent` 상속 |
| UI 교체 | 해당 `UIView`의 `Build()` 수정 |
