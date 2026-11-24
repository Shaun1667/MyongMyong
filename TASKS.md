# TASKS — MyongMyong (묭묭)

## 진행 요약 (2026-11-24 기준 — 새 세션은 여기부터 읽는다)
**최신 (2026-11-24): MY_DEV_GUIDE 적용 시작 — 문서 3종(CLAUDE · GDD · TASKS) 작성, Claude Hand 연결 완료. 캐릭터 이미지 파츠 나누기 진행 중(작업 폴더는 Claude 스크래치). 브랜치 `phase0-setup`(커밋 전), 테스트 0개. 다음: 0-1 텍스트 표 → 0-2 디버그 창 → 0-3 폴더 · 어셈블리 · 테스트.**

## Phase 0. 세팅
- [x] **0-0. 문서 3종** (2026-11-24 사용자 "MY_DEV_GUIDE 대입") — CLAUDE.md · GDD.md · TASKS.md
  - 결정 (2026-11-24, 사용자): 30분 생존 / PC + 모바일 / 한국어 + 영어
- [ ] **0-1. 텍스트 표** — 키 → 한국어 · 영어 표(CSV 또는 ScriptableObject), `Text.Get(키)`, 폰트(한글 + 영문) 확인, 테스트 3종(키 존재 · 기본 언어 · 코드에 한글 없음)
- [ ] **0-2. 디버그 창** — 메뉴 `Myong/Debug/Debug Window`, 첫 섹션: 재시작 · 시드 표시 · 지금 상태
- [ ] **0-3. 폴더 · 어셈블리 · 테스트** — CLAUDE.md 폴더 구조대로, Core는 noEngineReferences, EditMode 테스트 어셈블리
- [ ] **0-4. 데이터 에셋 자리** — Data/ 아래 첫 표(예: PlayerTuning) — "코드에 숫자 금지"
- [ ] **0-5. 2D 세팅** — URP 2D 렌더러, 픽셀 퍼펙트 카메라, PPU · 텍스처 임포트 규칙
- [ ] **0-6. Git** — 브랜치 `phase0-setup`, `.gitignore`의 `**/[Bb]uild/` · `**/[Bb]uilds/` 넓은 패턴을 루트 기준 경로로, `.gitattributes`(에셋 줄바꿈 고정)
- [ ] **0-7. MCP 연결 확인** — 2026-11-24 Claude Hand `unity-myongmyong` 연결됨(Interaction Mode No Throttling)

## Phase G. 그래픽 (병행)
- [ ] **G-1. 주인공 캐릭터 파츠 · 스켈레톤** (2026-11-24 사용자 "팔 · 다리 등 파츠를 나눠 스켈레톤 애니메이션") — 진행 중: 이미지 파츠 나누기 → 리깅 → 대기 · 달리기 · 피격

## 다른 PC에서 할 일
- Claude Hand 패키지(manifest.json)는 PC마다 `unity-install`로 넣는다.
