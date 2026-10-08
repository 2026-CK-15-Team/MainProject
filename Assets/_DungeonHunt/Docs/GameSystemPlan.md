# 게임 시스템 구현 계획서

> AI로 작성한 문서임. 기획서(00~10, 용어집, 미정파라미터 대장) 기준. 틀린 부분 있으면 바로 수정 바람.
> 11_프로토타입_빌드_프로필.md는 받은 자료에 없어서 **완성 목표(3층) 기준**으로 만들었음.

관련 문서: [SystemGuide.md](SystemGuide.md) (구조 설명) · [Manual.md](Manual.md) (사용법) · [EditorTaskList.md](EditorTaskList.md) (에디터 작업 목록)

---

## 1. 목표와 범위

| 구분 | 내용 |
|---|---|
| 목표 | 기획서 계약대로 동작하는 **완성 목표 런 1회** (첫 화면 → 로비 → 1~3층 → 최종보스 → 엔딩 상자 → 결과창 → 정산 → 로비) |
| 방식 | 기존 코드(던전 생성기, 무기, 이동)를 살리고 나머지 시스템을 얹음 |
| 아트 | 없음. 기본 도형/단색 타일로 정보 전달 (09 §3 "리소스 없으면 기본 도형, 색, 선, 점멸") |
| 수치 | 전부 기획서 `PrototypeDefault` (TODO_BALANCE). 임의 값 없음. `GameBalance` 에셋 한곳에 모음 |
| 콘텐츠 | 기획 `미정` 항목(아티팩트 개별 효과, 몬스터 구성, 보스 패턴 등)은 **Test_ 더미**로 자리만 채움 |

---

## 2. 기획 문서 → 코드 매핑

| 문서 | 시스템 | 주요 코드 | 상태 |
|---|---|---|---|
| 01-1 조작 | 이동/회피 FSM, 충전 스택, 저스트회피, 입력 우선순위, 히트스톱 입력 버퍼 | `Player/PlayerMovement`, `Movestate/*`, `PlayerInputReader`, `Core/InputGate` | 완료 |
| 01-2 전투와 무기 | 순환(라이플→권총→샷건), 백그라운드 재장전, 재장전 취소 발사, SwapReadyDelay, 발사 스냅샷, 치명타, 연속 충돌, 샷건 원추+LOS, 넉백, 저스트회피 탄 반환 | `Weapons/*` | 완료 |
| 01-3 생존 | Vulnerable/HitInvincible/Dead, 같은 틱 피해 합산, 사망 주체 공격 취소, 접촉 vs 능동 공격 | `Player/PlayerHealth` | 완료 |
| 01-4 스탯 | 고정→비율→(이속 하한)→상태 배율→상하한→반올림, 원천 단위 등록 | `Stats/*` | 완료 |
| 02 카메라 | 조준 오프셋(화면 좌표), 룸 경계 고정, 복도 무제한, 전투룸 진입 빠른 보간, 무방향 흔들림, 고정 줌(세로 6H) | `CameraSystem/CameraController` | 완료 |
| 03 던전 | 층 생성(기존 생성기), 룸 종류 매핑, RoomEntered/CorridorEntered, 층 전환, 미방문 룸 가림, 비밀 벽 | `Dungeon/*`(기존), `Rooms/FloorDirector`, `Rooms/FogOfWar`, `Rooms/SecretWall` | 부분 (§4 참고) |
| 03-1 전투룸 | 잠금 이펙트, 첫 웨이브 즉시 활성, 웨이브 예고, 접촉 유예, 클리어 순서, 보상 풀, 처치 재화 | `Rooms/CombatRoomController` | 완료 |
| 03-2 보물룸 | 최초 진입 아티팩트 1개 | `Rooms/TreasureRoomController` | 완료 |
| 03-3 상점 | 재고 1회 결정, 가중 가격, 슬롯 구성, 원자 구매, 영구재화 묶음 | `Rooms/ShopRoomController` | 완료 |
| 03-4 도박룸 | HP 비용, 천장형 확률, 예약 실패 시 롤백, Depleted | `Rooms/GambleSession`, `GambleRoomController` | 완료 |
| 03-5 비밀방 | 콘텐츠 사전 결정, 플레이어 공격 1회 파괴, 잠긴 룸 파괴 불가, 4종 콘텐츠 | `Rooms/SecretRoomController`, `SecretWall`, `Run/RunPlan` | 완료 |
| 03-6 층 종료 | EliteGate(2웨이브, 희귀 이상 보상), FloorExit, FinalBoss, RunEndTreasure(엔딩 상자) | `CombatRoomController`(IsEliteGate), `FloorExitController`, `FinalBossRoomController`, `RunEndTreasureController` | 완료 |
| 04 몬스터 | 5 아키타입, 공격 예약(t0, D 추첨, Armed, 경로 1~3), 방향 잠금, 사거리/LOS 표, 스트레이프, 넉백 곡선, 분리 이동, 경로 탐색(A*), 사망 시 탄 제거 | `Enemies/*` | 완료 |
| 05 보스 | Idle→Phase1→Phase2→Defeated, 조우 페이드 순서, 사망 모션 동안 비전투 | `Boss/*`, `RunManager.BossEncounterRoutine` | 구조 완료 (패턴은 미정 → 더미) |
| 06 런/저장 | 런 FSM, RunTime, 정산(정산 ID 1회 반영), 진행 해금 4조건, 영구 강화, 저장 시점, 저장 실패 2단계 팝업, 손상 세이브 초기화 | `Run/RunManager`, `Run/MetaProgression`, `Save/*` | 완료 (영구재화 상점은 잠정) |
| 07 아티팩트 | 6슬롯, 등급→ID 2단계 추첨, 제외 목록 3종, 빈 등급 재정규화, 세트 쏠림, 리롤(무료 1/유료 1), 획득 세션, 즉시 지급, 획득 대기, 버리기, 안전 배치, 세트 Tier, 무기 특화 잠금 | `Artifacts/*` | 완료 (개별 효과는 미정 → 스탯 수정값만) |
| 08 UI | 화면 상태 17종 대부분, 모달 정책 4그룹, ESC 처리 순서, 커서 제한, 포커스 이탈 정지, HUD 배치, 미니맵/확대 미니맵/순간이동, 설정(10초 유지 확인) | `UI/*` | 완료 (모양은 기본 도형) |
| 09 연출 | 피해 숫자(같은 공격 1개), 화이트 플래시, 적중/치명타 이펙트, 흔들림 중첩, 처치 히트스톱(공격당 1회), 공통 소멸 표현, 예고 도형, 잠금 이펙트 등장/소멸, 엔딩 상자 개봉, 아티팩트 획득, 세트 활성/해제 안내, 플레이어 이동/회피 잔상/사망 모션, 몬스터 이동/조준 방향 표시 | `Feedback/FeedbackService`, `Enemies/TelegraphView`, `Player/PlayerVisual`, `Enemies/Monster` | 완료 (임시 합성 사운드 `Audio/AudioService`) |
| 10 스토리 | 층 도착 안내(페이드 3단계, 일시정지 시 타이머 정지), 엔딩(마지막 대사, 첫 클리어 스킵 불가) | `UI/Views/HudView`, `MenuViews.EndingView`, `RunManager.EndingRoutine` | 완료 (엔딩 화면 형식 미정) |

---

## 3. 구현 순서 (실제 진행)

1. **코어**: 전투 시계(`GameClock`, 히트스톱/일시정지 분리), 이벤트 버스(`GameEvents`), 서비스 참조(`GameServices`), 벽 이동/판정 수학(`KinematicMover2D`, `Geometry2D`)
2. **설정**: `GameBalance`(전 수치), `GameConfig`, `FloorContent`, `MonsterWavePreset`
3. **플레이어**: 스탯 → 입력 → 이동/회피 → 생존 → 무기 → 상호작용 → 표현
4. **적**: 몬스터 데이터 → 공통 FSM → 아키타입 5종 → 투사체 → 경로 탐색 → 보스
5. **빌드**: 아티팩트 데이터/추첨/장착 → 재화 → 필드 보상
6. **공간**: 룸 런타임/지점 → 룸 종류별 컨트롤러 → 층 감독(FloorDirector) → 가림 → 비밀 벽
7. **흐름**: 런 계획 → RunManager(런 FSM, 층 전환, 순간이동, 보스 조우, 사망/포기/엔딩, 정산) → 세이브/메타
8. **표현**: 카메라 → 연출 → UI 전체
9. **에디터 도구**: 자리 표시용 콘텐츠 일괄 생성, 콘텐츠 검증기, 디버그 치트 창
10. **검증**: Unity 참조 DLL로 전체 컴파일, 생성기 + 자리 표시 방 크기로 900개 층 생성 테스트

---

## 4. 기획서와 다른 점 / 남은 공백

### 4.1 던전 생성 (03 §6.1) — 팀 생성기 사용 (확정)

기획서는 Edgar 포크 + 흐름 템플릿 방식이지만, **팀이 만든 격자 그래프 생성기를 쓰는 것으로 확정**. 생성기는 그대로 두고 룸 종류만 매핑함.

| 생성기 RoomType | 1, 2층 | 마지막 층 |
|---|---|---|
| Boss | EliteGateRoom | FinalBossRoom |
| Stairs | FloorExitRoom | RunEndTreasureRoom |

→ **마지막 층도 FloorConfig.HasStairs를 켜야** RunEndTreasureRoom이 생김 (기존 사용법 문서의 "3층 계단 끔" 추천과 다름).

생성기 쪽에 추가한 것 (생성 규칙은 그대로):
- 다음 층 배치 **미리 계산** (03 §6.2): 층 생성 직후 다음 층 배치를 백그라운드 스레드에서 계산, 층 전환 때는 오브젝트만 만듦. 안 끝났으면 검은 화면 유지 + 1초 넘으면 대기 표시. 같은 시드면 메인 스레드 계산과 결과 같음 (테스트 확인)
- **두 번째 UI 제작 폴백** (03-4 §2): `GameBalance.SecretRoom.SecondUIFallback` 켜면 도박방 0개. 추첨 순서는 그대로라 비밀방 개수/층 배정은 폴백 없을 때와 같음
- 비밀방 부모 입구 봉인, Blueprint 노출

Edgar 기반 규칙(X2 대형룸, 흐름 템플릿 하드 제약, 전투 환산 예산, 검증된 시드)은 이 생성기 범위 밖이라 적용 안 함. 대형룸은 `CombatRoomController.IsLarge`와 대형룸 보상 풀까지 코드만 준비됨.

### 4.2 기획 미정이라 더미로 둔 것

| 항목 | 처리 |
|---|---|
| 아티팩트 개별 효과, 세트 Tier 효과 (07 §9) | 스탯 수정값 목록만 지원. Tier 수정값 목록은 비어 있음. 특수 효과(관통, 저장 탄환 등)는 미구현 |
| 해금 출처별 아티팩트 ID (06 §5.3) | 테스트 카탈로그에 14/10/4로 임시 배정 |
| 몬스터 구성 프리셋, 스폰점 (03 §8.1) | Test_ 프리셋 (1층 4종 / 2층 6종 / 3층 8종, 엘리트 3종씩) |
| 최종보스 패턴 (05) | `BossDefinition.UsePlaceholderPatterns` 켜면 더미 패턴 (방사/조준 부채꼴/회전). 끄면 접촉 피해만 |
| 영구재화 상점 내부 화면 (06 §5.3, 08 §3) | 기본 스탯 강화 2종 + 재화 해금 아티팩트 목록으로 잠정 구성 |
| 엔딩 화면 형식 (10) | 검은 화면 + 마지막 대사 |
| RunEndTreasureRoom 일반 보물상자 내용물 | "비어 있음" 표시만 |
| 층 이름 (10 §5.4) | `GameBalance.FloorEnd.FloorNames` 임시 이름 |
| 사운드 / BGM (00 §9.1) | 사운드 방향 미정 → 런타임 합성 임시 효과음 20여 종 + 단순 BGM 루프. 설정 음량(전체/BGM/효과음) 연결됨. `AudioService`의 `overrides`에 같은 키로 실제 클립 연결하면 교체 |

### 4.3 미구현 / 간소화

| 항목 | 상태 |
|---|---|
| 11 프로토타입 빌드 프로필 (고정 7룸, 프로토타입 Tier1 4종, 플레이테스트 로그 JSON) | 문서 미수령 → 미구현. 여기에 딸린 권총 투사체 상쇄, 스왑 강화탄 HUD 외곽선, 라이플 스택 HUD도 미구현 |
| 계열 비율 검수 2·3층 (디자인_키워드 §5.2) | 문서 미수령 → 1층 수호자 금지만 검사 |
| 복합 콜라이더(CompositeCollider2D) | 런타임 생성 타일맵은 TilemapCollider2D만 씀 (동작 문제 없음, 최적화 항목) |
| 비전투 룸 가림 해제 페이드 중 플레이어 | 페이드(0.25초) 동안 덮개가 플레이어 위에 반투명으로 겹침. 전투룸은 즉시 해제라 해당 없음 |
| 캐릭터 재장전 모션 (09 미정) | 미구현. HUD 진행 표시만 |

### 4.3.1 이번에 추가 구현한 것 (마이너/비주얼)

| 항목 | 내용 | 코드 |
|---|---|---|
| 문 틈 표시 (03 §6.4) | 보이는 복도가 미방문 룸에 닿는 곳에 그 룸 바닥 경계 한 칸만 반투명 어둠. 비밀방 쪽은 없음. 반투명이라 진입선 직전까지 플레이어가 덮개 밑으로 사라지지 않음 | `Rooms/FogOfWar` |
| 16:9 필러박스/레터박스 (08 §7) | 게임 뷰 16:9 고정, 남는 곳 검은 띠. HUD/화면도 16:9 안(1920x1080 단위) | `CameraSystem/LetterboxCamera`, `UIManager.CreateCanvas` |
| 미니맵 종류 아이콘 (08 §4.1) | 상시/확대 미니맵 모두 외접 사각형 중심(깎인 칸이면 가까운 바닥 칸)에 종류 기호. 확대 미니맵은 옆에 남은 아이템 기호. 미니맵 룸 모양은 실제 바닥 타일 칸 | `UI/Views/MinimapViews`, `RoomRuntime.FloorCells` |
| HUD 무기 아이콘 / 잠금 | 무기 3종 실루엣, 잠긴 무기 흑백 + 자물쇠 아이콘 (문구 없음) | `PlaceholderArt.WeaponIcon/Lock`, `HudView` |
| 월드 오브젝트 실루엣 | 상자, 층 이동 포탈, 재화 동전, HP 하트, 아티팩트 마름모 | `PlaceholderArt` |
| 다음 층 미리 계산, 두 번째 UI 폴백 | §4.1 참고 | `DungeonBuilder`, `RunPlanner`, `FloorDirector`, `RunManager` |
| 연출 보강 (09 §3) | 적중/치명타 이펙트, 잠금 이펙트 등장/소멸, 엔딩 상자 개봉, 아티팩트 획득 파동, 세트 해제 안내, 플레이어 대기/이동/회피 잔상/사망, 몬스터 이동/정지 자세/조준 방향 | `FeedbackService`, `PlayerVisual`, `Monster`, `RoomDoor` |
| 임시 사운드 | §4.2 참고. UI 버튼 클릭음 포함 | `Audio/AudioService` |

### 4.4 검증 한계

- Unity 에디터를 돌려볼 수 없는 환경이라 **Unity 2021.3 참조 DLL + uGUI/InputSystem 스텁으로 컴파일만 확인**함. 실제 Unity 6000.3에서 열어 첫 플레이 확인 필요 ([EditorTaskList.md](EditorTaskList.md) 0단계).
- 생성기는 순수 C#이라 별도 콘솔 테스트로 확인: 자리 표시 방 크기로 시드 300개 x 3층 = 900개 층 전부 생성 성공, 비밀방 입구 계산 정상, 계단방은 항상 보스방 뒤 말단. 폴백 시 도박방 0 + 비밀방 배정 동일, 백그라운드 스레드 계산 결과 = 메인 계산 결과.

---

## 5. 다음 단계 제안 (우선순위)

1. **Unity에서 첫 실행 확인** → 컴파일 오류/런타임 예외 정리 (EditorTaskList 0단계)
2. 기획: 몬스터 구성 프리셋, 아티팩트 카탈로그를 실제 데이터로 교체 (Test_ 에셋 복제 후 수정)
3. 아트: 타일셋, 캐릭터 4방향 스프라이트, 몬스터 5종, 무기, UI 틀 → 각 정의 에셋에 연결
4. 프로그래밍: 프로토타입 프로필(11) 받으면 별도 프로필 구현, 아티팩트 특수 효과/보스 패턴 (기획 확정 뒤)
5. 사운드 방향 확정 뒤 `AudioService` 합성음을 실제 클립으로 교체
