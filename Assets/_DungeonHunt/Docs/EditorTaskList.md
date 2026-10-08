# 에디터 작업 목록

> AI로 작성한 문서임. 코드로 못 하는 Unity 에디터 작업 + 콘텐츠 작업. 위에서부터 순서대로.
> 담당: 💻 프로그래밍 · 🎨 아트 · 📝 기획

---

## 0단계 — 첫 실행 확인 (필수, 💻)

- [ ] Unity 6000.3에서 열고 **컴파일 오류 0개** 확인 (2021.3 참조 DLL로만 컴파일 검증함)
- [ ] Project Settings > Tags and Layers: Layer **`Wall`**, Tag **`Wall`** 있는지 확인 (없으면 추가)
- [ ] Project Settings > Player > Active Input Handling = **Input System Package (New)** 또는 Both
- [ ] **DungeonHunt > Setup > Generate Placeholder Content** 실행
- [ ] **DungeonHunt > Validate > All Content** → 오류 0개
- [ ] `Scenes/Game.unity` Play → 아래 흐름 한 번씩 확인
  - [ ] 첫 화면 → 로비 → 탐사 시작
  - [ ] 이동/회피/무기 3종/재장전/교체
  - [ ] 전투룸 잠금 → 웨이브 → 클리어 → 보상
  - [ ] 보물/상점/도박/비밀방
  - [ ] 엘리트 → 출구 → 층 전환 (x2)
  - [ ] 최종보스 → 엔딩 상자 → 엔딩 → 결과창 → 로비 (영구재화 증가)
  - [ ] 사망 / 런 포기 → 결과창
  - [ ] Debug Cheats "저장 실패 흉내" → 팝업 2단계
- [ ] 콘솔 예외 나오면 정리
- [ ] 파일명 대소문자 확인: `UI/WeaponReloadUI.cs`, `UI/PlayerHealthUI.cs` (기존 파일, 클래스명과 대소문자만 다름. 경고 나면 파일명 수정)
- [ ] 기존 `SampleScene`의 수동 배치 플레이어/UI가 새 시스템과 겹치는지 확인 → 정리하거나 `Game.unity`로 통일

## 1단계 — 프로젝트 설정 (💻)

- [ ] Sorting Layer 정리 (현재 sortingOrder 숫자로만 구분: 바닥 0 / 오브젝트 10~100 / 벽 115 / 벽 앞면 120 / 가림 1000)
- [ ] 벽 타일맵에 `CompositeCollider2D` 적용 검토 (런타임 생성 타일맵이라 `GameBootstrap.CreateTilemap`에서 추가)
- [ ] 해상도: 16:9 고정, 필러박스/레터박스 (08 §7, 미구현)
- [ ] 빌드 설정에 `Game.unity` 0번 확인
- [ ] (선택) `GameBootstrap`이 만드는 오브젝트를 씬에 미리 배치해 프리팹화

## 2단계 — 기획 데이터 (📝)

Test_ 에셋을 **Generated 밖으로 복제**해서 수정 (생성기 재실행 시 덮어씀).

- [ ] 몬스터 5종 확정 → `MonsterDefinition` (이름, HP, 속도, 아키타입별 값)
- [ ] 층별 몬스터 구성 프리셋 → `MonsterWavePreset` (1층 4 / 2층 6 / 3층 8 + 엘리트 각 3)
- [ ] 계열 비율 검수 규칙 (디자인_키워드 §5.2) → 검증기에 추가 요청
- [ ] 아티팩트 카탈로그 → `ArtifactDefinition` (ID, 등급, 해금 출처, 세트, 수정값)
- [ ] 세트 Tier 효과 → `ArtifactSetDefinition`
- [ ] 아티팩트 **특수 효과** 목록 → 💻 구현 요청 (스탯 수정값으로 안 되는 것)
- [ ] 최종보스 패턴 → `BossDefinition` 패턴 목록 (+ 💻 새 패턴 종류 구현)
- [ ] 층 이름, 층 도착 대사, 엔딩 대사 → `GameBalance.FloorEnd`, `UI`
- [ ] 엔딩 룸 일반 보물상자 내용물 결정
- [ ] 영구재화 상점 품목 확정
- [ ] 11_프로토타입_빌드_프로필 전달 → 💻 프로필 구현

## 3단계 — 방 제작 (🎨 + 📝)

- [ ] 실제 타일셋 → `Generated/Tiles` 대신 FloorConfig 타일 필드(복도 바닥, 위/아래/옆 벽, 아래 벽 앞면) + 방 프리팹 타일맵에 연결
- [ ] 방 프리팹 제작 ([RoomTemplateGuide.md](RoomTemplateGuide.md))
  - [ ] 각 방마다 **Add Missing Points** 후 지점 위치 조정
  - [ ] 전투룸 스폰점 수 ≥ 프리셋 요구 수
  - [ ] 엔딩 룸 `EndingChest` 1개, 출구 룸 룸 오브젝트 지점
  - [ ] Bake → FloorConfig 방 목록 등록
- [ ] 비밀 벽 표시 방식 확정 (현재 살짝 다른 색 타일)
- [ ] 미방문 룸 "문 틈 표시" 방식 확정 → 💻 구현

## 4단계 — 아트 연결 (🎨)

| 대상 | 연결 위치 | 현재 |
|---|---|---|
| 플레이어 4방향 | `PlayerVisual` 스프라이트 필드 | 원 + 방향 삼각형 |
| 무기 3종 | `WeaponScriptable.WeaponSprite`, `Icon` | 막대 |
| 플레이어 탄 | `WeaponScriptable.ProjectilePrefab` | 작은 원 |
| 몬스터 5종 | `MonsterDefinition` 프리팹/스프라이트 | 색 도형 (아키타입별 모양) |
| 보스 | `BossDefinition` 프리팹 | 큰 원 |
| 예고 도형 | `TelegraphView` | 반투명 선/부채꼴/원 |
| 아티팩트 | `ArtifactDefinition.Icon` | 등급 색 사각형 |
| 상자, 상점대, 도박대, 출구 | 각 룸 컨트롤러가 만드는 오브젝트 | 사각형 + 글자 |
| 재화/HP 보상 | `RewardPickup` | 노랑/초록 원 |
| 이펙트 (피격, 처치, 소멸, 잠금) | `FeedbackService` | 링 확산 |

- [ ] UI 틀/아이콘 → 각 `UIView.Build()` 교체 또는 프리팹화 (`UI/Views/*`)
- [ ] 폰트 → `GameConfig.UIFont` (지정 없으면 OS 폰트 자동 탐색)
- [ ] 미니맵 룸 종류 아이콘

## 5단계 — 사운드 (🎨 + 💻)

- [ ] 사운드 방향 확정 (00 §9.1 미정)
- [ ] 💻 오디오 매니저 추가 → `GameEvents` 구독으로 재생, 설정 BGM/효과음 음량 연결 (현재 저장만 됨)

## 6단계 — 프로그래밍 남은 것 (💻)

- [ ] 던전 생성기: 대형 전투룸(X2), 흐름 템플릿 하드 제약, 전투 환산 예산, 검증된 시드 복구 ([GameSystemPlan.md](GameSystemPlan.md) §4.1)
- [ ] 두 번째 UI 폴백 시 도박방 생성 안 하기
- [ ] 다음 층 배치 미리 계산
- [ ] 아티팩트 특수 효과 / 세트 효과 구현 (2단계 결과 받고)
- [ ] 보스 패턴 구현 (2단계 결과 받고)
- [ ] 플레이테스트 로그 (11 프로필)
- [ ] 첫 진입 시 층 경계~진입선 사이 가림 덮개가 잠깐 플레이어를 덮는 현상 확인
