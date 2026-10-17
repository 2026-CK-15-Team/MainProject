# 사용 매뉴얼

> AI로 작성한 문서임. 틀린 부분 있으면 바로 수정 바람.

구조는 [SystemGuide.md](SystemGuide.md), 남은 작업은 [EditorTaskList.md](EditorTaskList.md).

---

## 1. 처음 실행 (5분)

1. Unity로 프로젝트 열기 → 컴파일 끝날 때까지 대기
2. 메뉴 **DungeonHunt > Setup > Generate Placeholder Content**
   - `Assets/_DungeonHunt/Generated/` 아래 테스트 콘텐츠 전부 생성 (타일, 방 프리팹 12개, 층 설정 3개, 몬스터 5종, 웨이브 프리셋, 보스, 아티팩트 28개, 무기)
   - `Assets/_DungeonHunt/Resources/GameConfig.asset` 생성 (런타임이 여기서 설정을 읽음)
   - `Assets/_DungeonHunt/Scenes/Game.unity` 생성 + 빌드 설정 등록
3. **DungeonHunt > Validate > All Content** → 콘솔에 오류 0개 확인
4. `Game.unity` 열고 Play

> 다시 실행하면 Generated 폴더 에셋을 덮어씀. 직접 수정한 에셋은 Generated 밖으로 복제해서 쓸 것.

씬에 아무것도 없어도 `GameBootstrap`이 카메라, 타일맵, 매니저, 플레이어를 자동으로 만듦. 기존 `SampleScene`에서도 Play 가능 (`GameConfig`만 있으면 됨).

---

## 2. 조작

| 키 | 동작 |
|---|---|
| WASD | 이동 |
| 마우스 | 조준 |
| 좌클릭 | 공격 (누르고 있으면 연사) |
| Space | 회피 (충전 스택 소모, 무적) |
| Q / E | 이전 / 다음 무기 (라이플 → 권총 → 샷건) |
| R | 재장전 |
| F | 상호작용 (상자, 상점, 도박, 출구) |
| Tab | 확대 미니맵 (방문한 룸 클릭 → 순간이동, 전투 중 불가) |
| I | 보유 아티팩트 (버리기) |
| ESC | 닫기 / 일시정지 |

## 3. 런 진행

1. 첫 화면 → **게임 시작** → 로비
2. 로비: **탐사 시작** / 영구재화 상점 / 도감 / 설정
3. 1층 시작룸 도착 → 온보딩 안내 (복도로 나가면 완료)
4. 전투룸 들어가면 문 잠김 → 전멸시키면 열림 + 보상
5. 층 끝 엘리트룸(빨간 문) 클리어 → 출구 룸에서 F → 다음 층
6. 3층 끝 최종보스 → 엔딩 상자 F → 엔딩 → 결과창 → 로비로 (영구재화 정산)

미니맵 기호: `S` 시작 / `$` 상점 / `T` 보물 / `G` 도박 / `?` 비밀 / `E` 엘리트 / `B` 보스 / `>` 출구 / `*` 엔딩. 전투룸은 기호 없음. 확대 미니맵의 `◆ ● ♥` = 그 룸에 남은 아티팩트/재화/HP.

복도에서 보이는 미방문 룸 입구의 **어두운 한 줄(문 틈)** = 들어갈 수 있는 룸. 비밀방 쪽은 문 틈이 없음.

비밀방: 벽 중 조금 다른 색 칸을 총으로 쏘면 열림.

화면 비율이 16:9가 아니면 양옆/위아래에 검은 띠가 생김 (정상, 08 §7).

소리는 임시 합성음. 설정 > 전체/BGM/효과음 음량으로 조절.

---

## 4. 수치 조정

전부 **`Generated/Config/GameBalance.asset`** 하나에 있음 (기획서 `PrototypeDefault` 값).
인스펙터에서 필드에 마우스를 올리면 단위(H, 초, 도)와 계산 방식이 툴팁으로 나옴.

| 섹션 | 예시 |
|---|---|
| Player | 최대 HP, 이동 속도/가감속, 회피 거리/시간/충전, 저스트회피 판정, 무적 시간, 몸체/피격 반경, 치명타 기본값과 강화 단계 |
| Weapon | 발사 간격 하한, 무기별 이동 보정(권총 보너스, 라이플/샷건 감속), 저스트회피 탄 회복, 넉백 시간 |
| Camera | 화면 높이, 조준 오프셋, 추적 보간, 룸 이동 페이드 |
| CombatRoom | 보상 가중치/양 (일반, 대형룸), 웨이브 예고 시간, 접촉 유예, 보상 줍기 지연 |
| Shop / Gamble / SecretRoom | 가격 범위, 슬롯 확률, 리롤 비용 / 도박 HP 비용과 확률 누적 / 비밀방 콘텐츠 가중치 |
| FloorEnd / Boss | 대기 표시 지연, 층 이름 / 보스 기본 HP, 페이즈 전환 비율, 사망·등장 연출 시간, 접촉 피해 |
| Artifact | 슬롯 수, 세트 Tier 기준 개수, 등급 가중치, 세트 쏠림 확률, 리롤 횟수 |
| Meta | 룸/층 클리어 영구재화, 해금·강화 비용, 런 시작/결과/엔딩 연출 시간 |
| Feedback / UI | 흔들림 세기, 화이트 플래시, 히트스톱, 깜빡임 / HP바, 층 도착 문구, 미니맵 범위, 흔들림 설정 기본값 |

무기 수치는 `Generated/Weapons/*.asset` (`WeaponScriptable`: 피해, 연사, 탄창, 재장전, 사거리, 탄속, 원추 각도, 넉백, 교체 지연).
몬스터 수치는 `Generated/Monsters/*.asset` (`MonsterDefinition`: 아키타입을 고르면 기획 기본값이 들어감).
보스 수치와 패턴은 `BossDefinition` (패턴마다 종류, 간격, 예고 시간, 탄 수, 부채꼴 각도).

---

### 4.1 포스트프로세싱 (화면 효과)

프로필 하나에 전부 있음: **`Settings/PostProcess/GamePostProcessProfile.asset`** (인스펙터에서 조정, Play 중 바꿔도 바로 반영되고 에셋에 저장됨)

| 효과 | 컴포넌트 | 주요 수치 |
|---|---|---|
| 블룸 | URP `Bloom` | Threshold, Intensity, Scatter, Tint |
| 비네팅 | URP `Vignette` | Color, Intensity, Smoothness, Center |
| 색수차 | URP `Chromatic Aberration` | Intensity |
| 포그 | `DungeonHunt/Lens Effects` | 세기, 색, 크기(월드 칸), 흐름 속도, 대비, 가장자리 쏠림 |
| 프린징 | `DungeonHunt/Lens Effects` | 세기, 색, 폭(px), 문턱, 가장자리 쏠림 |
| 렌즈 플레어 | `DungeonHunt/Lens Effects` | 세기, 문턱, 색, 고스트 수/간격/색 번짐, 헤일로 세기/반지름, 가로 스트릭 세기/길이 |

- 효과 끄기: 세기 0 (계산도 건너뜀) 또는 컴포넌트 체크 해제
- 처음 설정 / 렌더러 기능이 빠졌을 때: **DungeonHunt > Setup > Setup Post Processing** (콘텐츠 생성기도 자동 호출)
- 구조: Bloom·Vignette·색수차는 URP 기본 후처리. 포그·프린징·렌즈 플레어는 풀스크린 셰이더 `Shaders/DungeonLensEffects.shader` 한 장을 URP 2D 렌더러의 **Full Screen Pass Renderer Feature**(After Rendering Post Processing)로 그림
- 구역별로 다르게 하려면 Local Volume(박스 콜라이더)을 두고 다른 프로필 지정 → 들어가면 블렌딩됨
- UI(HUD, 메뉴)에는 효과가 안 들어감

---

## 5. 콘텐츠 만들기

### 5.1 방 프리팹

1. 기존 Test 방 프리팹 복제 (또는 [RoomTemplateGuide.md](RoomTemplateGuide.md) 방식으로 새로 제작)
2. 방 루트 선택 → **DungeonHunt > Room > Add Missing Points To Selected Room**
   - 순간이동 도착점, 보상 생성점, 룸 오브젝트 지점, 몬스터 스폰점이 없으면 기본 위치로 추가
3. `RoomPoint` 위치 조정
   - 전투룸: 스폰점 ID는 웨이브 프리셋의 `SpawnPoint` 번호와 맞출 것
   - 엔딩 룸: 룸 오브젝트 역할 `EndingChest` 1개 필수
4. `RoomTemplate` 컴포넌트에서 Bake → `FloorConfig`의 방 목록에 추가

### 5.2 몬스터 웨이브 프리셋

`Create > DungeonHunt > Monster Wave Preset` (또는 `Generated/Presets` 복제)
- `Waves` 목록 → 웨이브마다 `Entries` (스폰점 번호, 몬스터, 처치 재화)
- 엘리트룸은 웨이브 2개
- 완성한 프리셋은 `FloorContent.CombatPresets` / `ElitePresets`에 넣기
- 방의 스폰점 개수가 부족하면 그 방에는 배정 안 됨 (`IsCompatible`)

### 5.3 아티팩트

실제 콘텐츠는 **`Content/Artifacts/`** (세트 6, 아티팩트 28). 목록과 수치는 [ArtifactContent.md](ArtifactContent.md)
- 표 수정 → `Scripts/Editor/ArtifactContentBuilder.cs` 데이터 수정 → **DungeonHunt > Content > Build Artifact Content** (같은 에셋에 값만 덮어씀, GameConfig 연결)
- 에셋을 직접 고쳐도 됨: `Modifiers`(스탯 수정값) + `Effects`(특수 효과: 교체 직후 피해, 도탄, 회피 버프, 저스트회피 무적, 라이플 스택, 라이플 사격 이동속도, 강화탄, 탄창 회복). 단 빌더를 다시 돌리면 빌더 데이터로 덮어씀
- 세트: Tier1/Tier2 수정값 + 특수 효과. **Tier2는 Tier1에 더해짐**
- 라이플 스택 유지 시간: `GameBalance > Artifact > Rifle Stack Duration` (기본 3초)
- 무기 특화 잠금: `GameBalance > Artifact > Weapon Specialization Rules` (기본 꺼짐)
- 새 아티팩트를 손으로 만들 때: `Create > DungeonHunt > Artifact` → `ArtifactCatalog` 목록에 추가

### 5.4 보스

`Generated/Monsters/Test_FinalBoss.asset`
- HP, 페이즈 전환 HP 비율, 패턴 목록
- `UsePlaceholderPatterns` 켜면 더미 패턴 사용 (패턴 기획 나오기 전까지)

### 5.5 층 구성

`Generated/Config/Test_Floor1~3.asset` (`FloorConfig`) — 방 목록, 전투룸 수, 순환 여부, 계단 유무.
`Generated/Config/Test_FloorContent1~3.asset` (`FloorContent`) — 그 층의 웨이브 프리셋, 엘리트 프리셋, 보스, 가림 색.
**3층도 HasStairs 켜야 엔딩 룸이 생김.**

### 5.6 타일셋 (코드 오토타일)

- `FloorConfig > Tileset`에 `DungeonTileset`을 넣으면 그 층의 벽·바닥 모양을 코드가 칸마다 고름 (방/복도 이음새 없이). 비우면 기존 5칸 방식
- 조각 목록과 ID: [ArtResourceList.md](ArtResourceList.md) 1장, 배치 예시: `ArtResourceList_TileSample.png`
- 방 프리팹은 **바닥 칸만 정확히** 칠하면 됨. 벽은 자동이고 프리팹 타일맵은 숨겨짐 → 같은 방을 층마다 다른 타일셋으로
- 빈 칸은 기본 칸으로 대체되니 아트를 일부만 넣어도 확인 가능. 테스트 생성기가 테두리 표시용 자리 표시 타일셋을 연결해 둠

---

## 6. 디버그

### 6.1 Debug Cheats 창

**DungeonHunt > Debug Cheats** (Play 중)

| 버튼 | 동작 |
|---|---|
| HP 회복 / 재화 +50 / 영구 재화 +100 | |
| 현재 룸 몬스터 전부 처치 | 클리어 흐름 바로 확인 |
| 다음 층으로 | 출구 룸 안 찾고 층 전환 |
| 저장 실패 흉내 켜기/끄기 | 저장 실패 팝업 2단계 확인 |

상단에 런 상태, 런 시드, 현재 층 표시. 같은 시드면 같은 맵.

### 6.2 세이브

- **DungeonHunt > Save > Delete Save File** — 처음부터 다시 (진행 해금, 영구재화 초기화)
- **DungeonHunt > Save > Open Save Folder** — `permanent.json`, `settings.json` 직접 확인

### 6.3 검증기

**DungeonHunt > Validate > All Content** 검사 항목: GameConfig 존재, 층 설정 3개, 방 프리팹 필수 지점, 방 Bake/문 콜라이더, 웨이브 프리셋 호환/1층 수호자 금지/엘리트 2웨이브, 몬스터 수치, 아티팩트 ID 중복/세트 4개/해금 출처 몫.

---

## 7. 자주 생기는 문제

| 증상 | 원인 / 해결 |
|---|---|
| Play 하면 "GameConfig 없음" | 1단계 생성기 안 돌림. `Assets/_DungeonHunt/Resources/GameConfig.asset` 확인 |
| 벽을 뚫고 지나감 | Layer `Wall` 없음 → Project Settings > Tags and Layers에 `Wall` 추가 후 생성기 다시 실행 |
| 전투룸에 몬스터 안 나옴 | 그 층 프리셋이 방 스폰점 수와 안 맞음 → 대체 스폰(기본 몬스터)으로 나옴. 검증기 오류 확인 |
| 3층에 엔딩 상자 없음 | Floor3 `HasStairs` 꺼짐 |
| 도박방이 안 나옴 | `GameBalance.SecretRoom.SecondUIFallback` 켜짐 (두 번째 UI 제작 폴백) |
| 층 전환 검은 화면이 김 | 다음 층 미리 계산이 안 끝남. 1초 넘으면 우하단 대기 표시. 콘솔에 "미리 계산 실패" 있으면 그 자리에서 다시 계산함 |
| 글자가 안 보임 | OS 폰트 못 찾음 → `GameConfig.UIFont`에 폰트 지정 |
| 일시정지 안 풀림 | 포커스 이탈 정지. 게임 창 클릭 |
| 블룸/비네팅이 안 보임 | URP가 꺼져 있음 → Project Settings > Quality의 Render Pipeline Asset이 `UniversalRP`인지 확인. 카메라 Post Processing 체크(런타임에 자동으로 켬) |
| 포그/프린징/플레어만 안 보임 | Renderer2D에 Full Screen Pass(DungeonLensEffects)가 없음 → Setup Post Processing 다시 실행 |
