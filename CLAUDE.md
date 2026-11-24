# CLAUDE.md — MyongMyong (묭묭)

## 프로젝트 개요
2D 도트 탑다운 뱀서라이크. 자동으로 쏘는 총을 든 소녀가 몰려오는 적을 피하며 30분을 버틴다. 레벨업마다 무기 · 패시브를 골라 빌드를 만든다. 참고: 《Vampire Survivors》(구조만 참고, 그대로 베끼지 않음).
기획은 GDD.md, 작업 순서와 일지는 TASKS.md, 수치는 BALANCE.md(첫 수치가 생기면 만든다). 작업 방식의 원본은 MY_DEV_GUIDE.md(= Docs/Source/MY_DEV_GUIDE.md, 고치지 않음).
- **새 세션을 시작하면** 이 문서 → TASKS.md 맨 위 "진행 요약" 순서로 읽고 다음 항목을 확인한 뒤 시작한다. 다른 PC면 Docs/진행상황.md도. "다음 작업: 미정"이면 후보를 먼저 묻는다.
- 수치는 BALANCE.md와 데이터 에셋 기준이며 코드에 임의로 넣지 않는다.
- Unity 프로젝트 폴더는 `MyongMyong/` (저장소 루트의 한 단계 아래).

## 참조 문서
| 문서 | 역할 | 상태 |
|---|---|---|
| GDD.md | 기획 | 초안 (2026-11-24) |
| TASKS.md | 진행 요약 · 작업 순서 · 일지 | 사용 중 |
| BALANCE.md | 수치 | 첫 수치 때 만든다 |
| MY_DEV_GUIDE.md | 협업 · 개발 방식 원본 | 고치지 않음 |
| MyongMyong/ClaudeHand/NOTES.md | Claude Hand 하위 세션 메모 | 사용 중 |

## 협업 방식
- 한국어. 1인 개발. 설계 결정은 선택지 + 추천안으로 묻고 `결정 (날짜, 사용자)`로 TASKS에 남긴다. 사소한 건 정하고 "정한 것 (Claude, 확인 필요)"로 보고.
- 한 항목 = 작업 브랜치(`phaseN-내용`) 커밋 + 보고. 지시 없이 main에 커밋 · 푸시하지 않는다. "main에 병합하고 푸시" = fast-forward 병합 · 푸시 · 작업 브랜치 삭제.
- 커밋 메시지 `MM.DD/Phase-항목/PascalCase요약`, 수정은 `…Fix`. 한 항목 = 한 커밋.
- 보고: 한 일 · 확인한 것 · 확인 못 한 것 · 확인 방법 · 커밋 상태 (+ 정한 것, 다음 추천).
- 테스트 실행은 사용자가 한다 (결정 2026-11-24, MY_DEV_GUIDE 기본값) — Claude는 작성만, 보고에 "테스트 작성, 실행 안 함". 몇 항목마다 "테스트 돌릴 때 됐다"고 알린다.
- 완료 = 테스트 통과 + 확인 방법 + 사용자 플레이 확인. 구현만 = `— 플레이 확인 대기`.

## 작업 원칙
- 한 번에 기능 하나 (TASKS 현재 항목만). 요청받지 않은 시스템을 미리 만들지 않는다 — 단 핵심 설계 규칙의 구조는 처음부터.
- 구현 전 문서를 읽고 구현 후 문서를 고친다. 다르면 문서를 실제 기준으로 고치고 알린다.
- 에디터 수작업은 목록으로 안내, 반복 생성은 멱등 Setup 메뉴로.
- 실패 · 생략 · 미확인은 그대로 보고.

## 기술 스택
| 항목 | 값 | 상태 |
|---|---|---|
| 엔진 | Unity 6000.3.25f1 | 확정 2026-11-24, 변경 금지 |
| 렌더 | URP 17.3 · 2D 렌더러(`Assets/_Game/Settings/Myong2D_RPAsset.asset`, 그래픽 기본값 + 모든 품질 단계에 지정) | 확정 2026-11-24 |
| 입력 | Input System 1.20 (키보드 · 패드 · 가상 조이스틱) | 확정 2026-11-24, 변경 금지 |
| UI | uGUI | 정한 것 (Claude, 확인 필요) |
| 플랫폼 | PC + 모바일 | 결정 2026-11-24, 사용자 |
| 언어 | 한국어(기본) + 영어 | 결정 2026-11-24, 사용자 |
| 그래픽 | 2D 도트, 캐릭터는 파츠 컷아웃 스켈레톤 애니메이션 | 2026-11-24 사용자 |
| 저장소 | GitHub Shaun1667/MyongMyong | — |

## 폴더 구조 (Phase 0에서 만든다)
```
MyongMyong/Assets/_Game/
  Scripts/Core/      규칙 코드 — 엔진 참조 없음 (Myong.Core.asmdef, noEngineReferences)
  Scripts/Unity/     엔진 어댑터 · 표시 (Myong.Unity.asmdef → Core 참조)
  Scripts/Editor/    에디터 도구 · Setup 메뉴 · 디버그 창 (Myong.Editor.asmdef, Editor 전용)
  Data/              ScriptableObject 데이터 에셋 (수치 · 콘텐츠 표)
  Text/              텍스트 표 (키 → 한국어 · 영어)
  Art/               도트 · 캐릭터 파츠 · 애니메이션 (여기 텍스처는 임포트 규칙이 자동 적용)
  Prefabs/           프리팹 (Player.prefab — Setup 메뉴가 만든다)
  Settings/          2D 렌더러 · 파이프라인 에셋
  Scenes/
  Tests/EditMode/    단위 테스트 (Myong.Tests.asmdef)
```

## 코딩 규칙
- 네임스페이스 `Myong.Core.*` / `Myong.Unity.*` / `Myong.Editor.*`.
- 화면에 보이는 모든 글은 텍스트 표 한곳 — 코드 · 프리팹 · 데이터에 문장 금지, 코드는 `Text.Get(키)`, 문장을 이어 붙이지 않고 `{0}` 자리표시자 (MY_DEV_GUIDE 1번). 키 규칙 `분류.대상.용도`.
- 개발자 디버그 기능은 에디터 디버그 창(메뉴 `Myong/Debug/Debug Window`)에 섹션으로, 게임 쪽 진입점은 `GameRoot.DebugXxx`, 상태를 바꾸면 [상태 변경] 표시 (MY_DEV_GUIDE 2번).
- 입력은 한 곳에서만 읽는다(PC 키보드 · 패드 · 모바일 가상 조이스틱을 같은 이동 벡터로).
- 텍스트는 `Myong.Core.Text` 한 곳, 데이터 `Assets/_Game/Text/strings.csv`(key,ko,en), 새 글 = 표 한 줄. 화면 라벨은 `LocalizedLabel`에 키만, 글꼴은 `UiFont` 한 곳. UI 코드에서 `using UnityEngine.UI`를 쓰면 `Text` 이름이 겹치니 `UnityEngine.UI.Text`로 풀어 쓴다.
- 씬 연결은 `GameRoot` 한 곳(컴포지션 루트): 표 읽기 · 판(World) 만들기 · 고정 틱 돌리기 · 표시 쪽에 상태 넘기기. 싱글턴을 만들지 않는다.
- 시뮬레이션은 `Myong.Core.Simulation.Step` 한 곳(한 번 = 한 틱), 상태는 `World`에만. 틱 설정은 데이터 `Data/SimulationTuning.asset`.
- 이동 규칙은 `Simulation.Step` + `MoveRules`(대각선 길이 1), 수치는 데이터 `Data/PlayerTuning.asset`(이동 속도). Core의 위치는 `Vec2`.
- 입력은 `PlayerInputReader` 한 곳 — `Assets/InputSystem_Actions.inputactions`의 Player/Move 액션. 가상 조이스틱도 이 액션에 묶는다(코드에서 키를 직접 읽지 않는다).
- 플레이어 그리기는 `PlayerView` 한 곳(보간 · 도트 격자 맞춤 · 좌우 뒤집기 · 애니메이터 파라미터). 이동은 Core가 하고 애니메이터에는 파라미터(Speed · Hit)만.
- 도트 기준(PPU 32 · 기준 해상도 960x540)은 `PixelArt` 한 곳. 임포트 규칙은 `PixelArtImportRules`(`Assets/_Game/Art/` 아래 자동).
- 주인공 리그 · 애니메이션은 `PlayerRigBuilder`의 Bones · Clips 표가 기준 — 프리팹 · 클립 · 컨트롤러를 손으로 고치지 말고 표를 고친 뒤 메뉴 `Myong/Setup/Build Player Rig`. 새 뼈 · 새 프레임 = 표 한 줄.
- 씬 · 에셋 구성은 Setup 메뉴(`Myong/Setup/Run All` = 2D 렌더 설정 → 데이터 에셋 → 아트 재임포트 → 리그 → Game 씬)로, 몇 번 돌려도 같은 결과. 수치 에셋은 없을 때만 만들고 값은 덮어쓰지 않는다.
- `Myong.Editor` 네임스페이스 안에서는 `Editor`가 네임스페이스로 읽힌다 — 커스텀 인스펙터는 `UnityEditor.Editor`로 풀어 쓴다.
- (시스템이 생길 때마다 한 줄씩: "○○는 `클래스` 한 곳, 데이터 `에셋`, 새 항목 = 표 한 줄")

## 핵심 설계 규칙 (변경 금지)
바꾸고 싶어지면 영향 범위를 표로 정리하고 사용자 결정을 받은 뒤에만.
1. 규칙과 표현 분리 — 적 · 탄 · 피해 · 경험치 · 레벨업은 Core, Unity는 그리기만.
2. 고정 틱 하나 — 시뮬레이션은 고정 틱(예: 60Hz)으로만 진행, 표시는 보간.
3. 상태는 스냅샷 하나 — 한 판의 상태는 World에.
4. 시드 난수 주입 · 용도별 분리 — 스폰 ≠ 레벨업 선택지 ≠ 드롭 ≠ 연출. 같은 시드 = 같은 결과.
5. 모든 엔티티 고유 ID.
6. 데이터 주도 — 새 무기 · 적 · 패시브 = 표 한 줄, 종류별 분기 코드 금지.
7. 적 수백 마리를 전제로 — Core는 할당 없는 배열 · 풀, Unity는 풀링된 렌더러(나중에 GPU 인스턴싱도 열어 둠).

## 아트 · 그래픽 규칙
- 도트: Point 필터, 압축 없음, 밉맵 끔, PPU 32 통일, 기준 해상도 960x540, 픽셀 퍼펙트 카메라(Upscale Render Texture) — 값은 `PixelArt`, 임포트는 `PixelArtImportRules`가 자동(정한 것 2026-11-24, Claude, 확인 필요).
- 캐릭터: 파츠(뒷머리 · 머리 · 몸통 · 팔 · 총 · 다리)를 뼈마다 통째로 붙이는 컷아웃 방식 — 메시 변형(스키닝)은 도트를 뭉개므로 쓰지 않는다. 파츠 위치 · 관절은 `Parts/layout.json`, 움직임은 도트 한 칸 단위 계단식 키, 스프라이트 재질은 Sprite-Unlit.
- 데이터는 비주얼을 ID로만 참조, 없으면 단색 도형으로 그린다.

## Git · 환경
- 커밋 전 `git status` → 내 파일만 `git add` → `git diff --cached --stat`. 다른 세션 변경 제외.
- **`MyongMyong/Packages/manifest.json`의 Claude Hand 패키지 줄(com.claudehand.tools 로컬 경로, com.coplaydev.unity-mcp)과 `manifest.json.claude-hand.bak`은 커밋하지 않는다** (이 PC 경로가 들어 있음).
- Claude Hand 하위 세션 `unity-myongmyong`으로 에디터를 다룬다. 파일을 바꾼 직후 첫 호출은 가벼운 것(콘솔 읽기)으로.
