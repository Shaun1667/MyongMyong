# TASKS — MyongMyong (묭묭)

## 진행 요약 (2026-11-24 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-24): Phase 0(0-1 ~ 0-5) · G-1 주인공 리깅 · 1-1 WASD 8방향 이동 구현 — 모두 플레이 확인 대기. Game 씬 `Assets/_Game/Scenes/Game.unity`. 브랜치 `phase0-setup`(커밋 전), 테스트 21개 작성(실행 안 함 — 돌릴 때 됐다). 다음: 사용자 플레이 확인 · 테스트 실행 → 다음 항목 미정(후보를 먼저 묻는다: 적 스폰 · 자동 사격 · 가상 조이스틱).**

## Phase 0. 세팅
- [x] **0-0. 문서 3종** (2026-11-24 사용자 "MY_DEV_GUIDE 대입") — CLAUDE.md · GDD.md · TASKS.md
  - 결정 (2026-11-24, 사용자): 30분 생존 / PC + 모바일 / 한국어 + 영어
- [x] **0-1. 텍스트 표** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: 표 `Assets/_Game/Text/strings.csv`(key,ko,en · `#` 주석 · 따옴표 칸) · `Myong.Core.Text`(Get(키) · Get(키, 값…) `{0}` 자리표시자 · SetLanguage · Changed) · `TextTable`(CSV 읽기) · `LocalizedLabel`(키 하나를 보여 주는 라벨) · `UiFont`(글꼴 한 곳) · 읽기는 `GameRoot.Awake`
  - 정한 것 (Claude, 확인 필요): CSV 방식 / 없는 문장은 기본 언어(ko) → 그것도 없으면 키를 그대로 표시 / 글꼴은 파일 없이 기기 글꼴로 동적 생성(Malgun Gothic → Noto Sans CJK KR → … 후보 순) / HUD는 uGUI 기본 Text
  - 확인: 컴파일 오류 0 · Game 씬 Play에서 좌상단 라벨 ko "묭묭 준비 완료!" ↔ en "MyongMyong is ready!" 전환 · 캡처 `MyongMyong/Screenshots/01_idle_ko.png` · `02_idle_en.png`. 확인 못 함: 모바일(Android · iOS) 기기에서 한글 글꼴이 잡히는지
  - 테스트 작성, 실행 안 함: `TextTableTests`(쓰는 키가 표에 있음 · 기본 언어 문장 있음 · 모든 언어 칸 있음 · 게임 코드에 한글 문자열 없음 · CSV 따옴표 · 대체 순서 · 자리표시자 · 언어 전환 알림)
  - 확인 방법: Game 씬 Play → 좌상단에 글 → 메뉴 Myong/Debug/Debug Window → 텍스트 섹션의 Ko / En 버튼으로 글이 바뀌면 정상
- [x] **0-2. 디버그 창** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `MyongDebugWindow`(메뉴 `Myong/Debug/Debug Window`, 섹션 목록 구조) · 섹션: 판 · 시드(시드 · 틱 표시, [상태 변경] 재시작 · 시드 정해 재시작) / 플레이어(위치 · 속도 · 방향, [상태 변경] 순간이동 · 입력 대신 넣기 8방향, 피격 애니메이션 보기) / 텍스트(언어 전환) · 게임 쪽 진입점 `GameRoot.DebugXxx`
  - 확인: 메뉴로 창이 열림(코드로 열고 닫음) · DebugXxx 진입점으로 플레이 진행. 확인 못 함: 창의 버튼을 직접 누르는 것, 창 모양
  - 확인 방법: Game 씬 Play → Myong/Debug/Debug Window → 값이 보이고 버튼이 동작하면 정상 (Play가 아니면 안내 글만 나온다)
  - 남은 것: 기록 · 순위가 생기면 "디버그를 쓴 판은 기록에 남기지 않기" 표시를 붙인다
- [x] **0-3. 폴더 · 어셈블리 · 테스트** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `Assets/_Game/` 아래 Scripts/Core · Unity · Editor, Data, Text, Art, Scenes, Prefabs, Settings, Tests/EditMode · `Myong.Core.asmdef`(noEngineReferences) · `Myong.Unity.asmdef`(→ Core, Input System, UI) · `Myong.Editor.asmdef`(Editor 전용) · `Myong.Tests.asmdef`(EditMode)
  - 정한 것 (Claude, 확인 필요): 폴더 Prefabs · Settings 추가(CLAUDE.md 폴더 구조에 반영)
  - 확인: 컴파일 오류 0, 네 어셈블리 로드됨
  - 테스트 작성, 실행 안 함: `ProjectRulesTests.CoreAssembly_DoesNotReferenceTheEngine`
- [x] **0-4. 데이터 에셋 자리** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: `PlayerTuning`(이동 속도) · `SimulationTuning`(초당 틱 · 한 프레임 최대 틱) · 에셋 `Assets/_Game/Data/PlayerTuning.asset` · `SimulationTuning.asset` · 메뉴 `Myong/Setup/Create Data Assets`(없을 때만 만들고 값은 덮어쓰지 않음)
  - 정한 것 (Claude, 확인 필요): 이동 속도 4.5유닛/초(= 144도트/초, 화면 가로를 약 6.7초에 지남) · 60틱/초 · 한 프레임 최대 5틱 — BALANCE.md는 아직 없음(전투 수치가 생길 때 만들고 이 값도 옮긴다)
  - 테스트 작성, 실행 안 함: `ProjectRulesTests.PlayerTuning_…` · `SimulationTuning_…`
- [x] **0-5. 2D 세팅** (2026-11-24 사용자 "두 프로젝트의 페이즈 0 진행") — 플레이 확인 대기
  - 구현: 2D 렌더러 `Assets/_Game/Settings/Myong2D_Renderer.asset` + 파이프라인 `Myong2D_RPAsset.asset`를 새로 만들어 그래픽 기본값과 품질 단계(Mobile · PC) 모두에 지정 — 템플릿의 `Assets/Settings/PC_* · Mobile_*`는 그대로 둠 · 메뉴 `Myong/Setup/Apply 2D Render Settings` · 도트 기준 `PixelArt`(PPU · 기준 해상도 한 곳) · 임포트 규칙 `PixelArtImportRules`(`Assets/_Game/Art/` 아래 텍스처: 스프라이트 · PPU 32 · Point · 압축 없음 · 밉맵 끔 · 처음엔 Single) · 카메라에 Pixel Perfect Camera · 씬 `Assets/_Game/Scenes/Game.unity`(메뉴 `Myong/Setup/Build Game Scene`, 전부 한 번에 = `Myong/Setup/Run All`)
  - 정한 것 (Claude, 확인 필요): PPU 32 · 기준 해상도 960x540(1080p에서 2배, 4K에서 4배; 캐릭터 키 79도트 ≈ 화면 높이의 15%) — 640x360이면 캐릭터가 화면 높이의 22%라 적 수백 마리 화면에 좁다고 봄 / Pixel Perfect Camera는 Upscale Render Texture(회전한 파츠도 도트 격자에 다시 맞춰짐) / 스프라이트 재질은 Sprite-Unlit(2D 조명 안 씀) / 빌드 씬 목록 맨 앞에 Game 씬 추가(SampleScene은 남김) / 2D 렌더러의 후처리 데이터는 비어 있음(후처리 안 씀)
  - 확인: 컴파일 오류 0 · Play 캡처에서 도트가 2배 정수 확대로 선명 · Setup을 다시 돌려도 프리팹 · 씬 · 애니메이션 파일이 그대로(해시 비교). 확인 못 함: 1080p 아닌 해상도 · 창 크기 바꿀 때 · 모바일 화면
  - 확인 방법: Game 씬 Play → 캐릭터 · 바닥 도트가 뭉개지지 않고 또렷하면 정상
- [x] **0-6. Git** (2026-11-24) — 브랜치 `phase0-setup` · `.gitignore` 빌드 패턴을 루트 기준(`/MyongMyong/[Bb]uild/` · `/MyongMyong/[Bb]uilds/` · `/[Bb]uilds/`) · `/MyongMyong/Screenshots/` 무시 · `.gitattributes`(코드 CRLF, Unity YAML LF + unityyamlmerge, 이미지 바이너리). manifest.json · 템플릿 RP 에셋의 자동 버전 업 · SceneTemplateSettings.json은 커밋 제외
- [x] **0-7. MCP 연결 확인** — 2026-11-24 Claude Hand `unity-myongmyong` 연결됨(Interaction Mode No Throttling)

## Phase G. 그래픽 (병행)
- [x] **G-1. 주인공 캐릭터 파츠 · 스켈레톤** (2026-11-24 사용자 "묭묭 파츠로 리깅 진행해서 WASD로 상하좌우 그리고 대각선까지 이동되게 해줘") — 플레이 확인 대기
  - 구현: `PlayerRigBuilder`(메뉴 `Myong/Setup/Build Player Rig`, 몇 번 돌려도 같은 결과) — `Parts/layout.json`의 위치 · 관절 · 그리는 순서로 스프라이트 피벗을 맞추고 뼈 계층을 만든다: Player(Animator · SortingGroup · PlayerView) → Root(좌우 뒤집기) → Hips → Torso → Head → HairBack / Torso → ArmBack, WeaponArms → Gun · ArmFront / Root → LegL, LegR · 프리팹 `Assets/_Game/Prefabs/Player.prefab` · 클립 `Art/Characters/Player/Animations/Player_Idle · Player_Run · Player_Hit.anim` · `Player.controller`(Speed로 Idle ↔ Run, Hit 트리거)
  - 방식: 컷아웃(파츠를 뼈에 통째로, 메시 변형 없음, 패키지 추가 없음) · Transform 키만 · 모든 움직임은 도트 한 칸 단위 계단식 키 · 좌우는 Root X 스케일 -1
  - 정한 것 (Claude, 확인 필요): 총 + 앞팔은 따로 붙임(gun · arm_front; weapon_arms · weapon_arms_both 합본은 안 씀) / 다리는 Root 바로 아래(몸통이 통통 튀어도 발은 땅에) / Idle 8프레임 6fps, Run 8프레임 14fps, Hit 4프레임 12fps / 다리 회전 최대 14도 · 총 2도 / 뼈 · 애니메이션 표는 `PlayerRigBuilder`의 Bones · Clips
  - 확인: 파츠 좌하단이 layout.json의 정수 좌표와 모두 일치(코드로 대조) · Play에서 Idle ↔ Run 전환 · 왼쪽 이동 때 뒤집힘 · 캡처 `Screenshots/07_anim_idle_frames.png` · `08_anim_run_frames.png` · `09_anim_hit_frames.png`. 확인 못 함: 움직이는 모습(정지 그림으로만 봄) — 속도감 · 어색함은 사용자 눈으로
  - 확인 방법: Game 씬 Play → 가만히 있으면 숨쉬기, WASD로 달리기, A로 왼쪽을 보면 정상. 피격은 디버그 창 플레이어 섹션의 "피격 애니메이션 보기"
  - 남은 것: 피격 깜빡임(흰색/빨강) · 사망 애니메이션
  - 고침 (2026-11-24, 사용자 "다리가 더 걷는 느낌"): 달리기에서 다리가 제자리에서 살짝 돌기만 해 걷는 느낌이 약함 → 원인: 다리 키가 회전(최대 14도) + 위아래뿐이고 앞뒤 이동이 없었음 → 수정: `PlayerRigBuilder` Clips 표의 Player_Run을 걸음 8프레임(0 접촉 · 1 내려앉음 · 2 지나감 · 3 올라감 · 4 반대쪽 접촉 …)으로 다시 짬 — 딛는 다리는 낮게 +3 → -3칸 뒤로, 뜬 다리는 1~2칸 들려 앞으로, 회전은 최대 8도로 줄임 / 몸통은 내려앉을 때 -1 · 올라갈 때 +1 / 머리는 1프레임, 뒷머리는 2프레임 늦게 / 총 · 팔은 내려앉을 때 한 칸 늦게(총 회전은 뺌) / 14fps → 16fps(초당 4걸음). Idle · Hit은 그대로. 확인: Setup 다시 실행 후 Play 캡처 `Screenshots/10_anim_run_walk_frames.png` · `11_run_before_after.png` · `12_run_legs_closeup_before_after.png`(위 = 이전, 아래 = 지금). 확인 못 함: 움직이는 모습, 발 미끄러짐 정도 — 다리가 짧아(한 걸음 약 8도트) 이동 속도 144도트/초에 발을 완전히 맞출 수는 없음. 검사: 다리 느낌을 볼 때는 12번처럼 다리만 확대한 프레임 시트로

## Phase 1. 구조 기반 + 핵심 조작
- [x] **1-1. 이동 (WASD 8방향)** (2026-11-24 사용자 "WASD로 상하좌우 그리고 대각선까지 이동되게 해줘") — 플레이 확인 대기
  - 구현: Core `Vec2` · `World`(Seed · Tick · NextEntityId · Player) · `PlayerState` · `Simulation`(고정 틱 Step · CreateWorld · TeleportPlayer) · `MoveRules.ClampMove`(대각선을 길이 1로) / Unity `GameRoot`(틱 누적 · 보간 비율) · `PlayerInputReader`(입력 한 곳: `Assets/InputSystem_Actions.inputactions`의 Player/Move — WASD · 방향키 · 패드 왼스틱) · `PlayerView`(보간 + 도트 격자 맞춤 · 뒤집기 · Speed) · `CameraFollow` · `GroundGrid`(임시 격자 바닥 `Art/Temp/ground_grid.png`)
  - 정한 것 (Claude, 확인 필요): 위(W)·아래(S)로만 갈 때는 마지막 좌우 방향 유지 / 스틱을 살짝 밀면 그만큼 느리게(1 미만은 그대로) / 카메라는 플레이어에 고정(부드러운 따라가기 없음) / 난수 클래스는 첫 사용처(적 스폰) 때 만든다 — 지금은 World.Seed만
  - 수치: `Assets/_Game/Data/PlayerTuning.asset` 이동 속도 4.5
  - 확인: Play에서 `GameRoot.DebugSetMoveInput`으로 8방향 각 59틱 → 모두 거리 4.425(= 4.5 × 59/60), 대각선 (3.129, 3.129) · 속도 크기 4.5 동일 · 표시 위치와 카메라가 정수 도트 · 입력을 풀면 Idle · 캡처 `Screenshots/03_run_right.png` · `04_run_left.png` · `05_run_diag_upright.png` · `06_run_diag_downleft.png`. 확인 못 함: 실제 키보드 · 패드 입력(에디터에 포커스가 없어 프레임을 일시정지 + Step으로 돌렸고, 그 상태에서는 Input System이 키 이벤트를 게임에 넘기지 않음 — Move 액션에 W/A/S/D · 방향키 · 패드 왼스틱이 묶여 있고 켜져 있는 것까지만 확인)
  - 테스트 작성, 실행 안 함: `MoveTests`(대각선 속도 = 직선 속도 · 8방향 같은 속도 · n틱 후 위치 · 스틱 절반 · 입력 없으면 정지 · 직전 위치 · 방향 · 순간이동 · 같은 입력 같은 결과 · 엔티티 ID)
  - 확인 방법: Game 씬 Play → W/A/S/D와 방향키로 상하좌우, 두 키를 같이 눌러 대각선 — 바닥 격자가 흘러가고 대각선이 더 빠르지 않으면 정상. 디버그 창 플레이어 섹션에서 위치 · 속도(|v| = 4.5) 확인

## 다른 PC에서 할 일
- Claude Hand 패키지(manifest.json)는 PC마다 `unity-install`로 넣는다.
