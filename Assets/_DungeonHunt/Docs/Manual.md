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
| Tab | 확대 미니맵 (방문한 비전투 룸 클릭 → 순간이동) |
| I | 보유 아티팩트 (버리기) |
| ESC | 닫기 / 일시정지 |

## 3. 런 진행

1. 첫 화면 → **게임 시작** → 로비
2. 로비: **탐사 시작** / 영구재화 상점 / 도감 / 설정
3. 1층 시작룸 도착 → 온보딩 안내 (복도로 나가면 완료)
4. 전투룸 들어가면 문 잠김 → 전멸시키면 열림 + 보상
5. 층 끝 엘리트룸(빨간 문) 클리어 → 출구 룸에서 F → 다음 층
6. 3층 끝 최종보스 → 엔딩 상자 F → 엔딩 → 결과창 → 로비로 (영구재화 정산)

맵 색: 시작 회색 / 전투 빨강 / 보물 노랑 / 상점 초록 / 도박 보라 / 비밀 청록 / 엘리트·보스 진빨강 / 출구 흰색 (미니맵 기준)

비밀방: 벽 중 조금 다른 색 칸을 총으로 쏘면 열림.

---

## 4. 수치 조정

전부 **`Generated/Config/GameBalance.asset`** 하나에 있음 (기획서 `PrototypeDefault` 값).

| 섹션 | 예시 |
|---|---|
| Player | 최대 HP, 이동 속도, 회피 거리/시간/충전, 무적 시간, 몸체 반경 |
| Weapon | 저스트회피 판정, 치명타 기본값, 무기 교체 지연 |
| Camera | 조준 오프셋, 보간 속도, 흔들림 세기 |
| CombatRoom | 웨이브 예고 시간, 접촉 유예, 보상 확률/양 |
| Shop / Gamble / SecretRoom | 가격 후보와 가중치, 도박 HP 비용/확률, 비밀방 콘텐츠 확률 |
| FloorEnd / Boss | 층 클리어 보너스, 층 이름, 보스 페이드 시간 |
| Artifact | 등급 가중치(획득 경로별), 리롤 가격, 세트 쏠림 확률 |
| Meta / Feedback / UI | 진행 해금 조건, 피해 숫자, 히트스톱, 페이드 시간 |

무기 수치는 `Generated/Weapons/*.asset` (`WeaponScriptable`: 피해, 연사, 탄창, 재장전, 사거리, 탄속, 원추 각도, 넉백).
몬스터 수치는 `Generated/Monsters/*.asset` (`MonsterDefinition`: 아키타입을 고르면 기획 기본값이 들어감).

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

`Create > DungeonHunt > Artifact`
- ID(고유), 이름, 등급, 해금 출처, 세트(선택), `Modifiers`(스탯 수정값)
- `ArtifactCatalog` 에셋 목록에 추가
- 세트: `Create > DungeonHunt > Artifact Set` → Tier별 수정값, 무기 특화면 특화 무기 지정

### 5.4 보스

`Generated/Monsters/Test_FinalBoss.asset`
- HP, 페이즈 전환 HP 비율, 패턴 목록
- `UsePlaceholderPatterns` 켜면 더미 패턴 사용 (패턴 기획 나오기 전까지)

### 5.5 층 구성

`Generated/Config/Test_Floor1~3.asset` (`FloorConfig`) — 방 목록, 전투룸 수, 순환 여부, 계단 유무.
`Generated/Config/Test_FloorContent1~3.asset` (`FloorContent`) — 그 층의 웨이브 프리셋, 엘리트 프리셋, 보스, 가림 색.
**3층도 HasStairs 켜야 엔딩 룸이 생김.**

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
| 글자가 안 보임 | OS 폰트 못 찾음 → `GameConfig.UIFont`에 폰트 지정 |
| 일시정지 안 풀림 | 포커스 이탈 정지. 게임 창 클릭 |
