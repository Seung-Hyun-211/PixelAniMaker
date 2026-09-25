# 패치노트 — 반측면 ①: 방향 모델

날짜: 2026-09-25 · 브랜치: `claude/work-environment-setup-a3u93e` · 계획: [plan-three-quarter-view.md](../plan-three-quarter-view.md) 9절 ①

반측면(3/4 뷰)을 담을 수 있도록 Core의 방향 모델을 넓혔다. **화면·결과물은 바뀌지 않는다** — 반측면을 켜는 UI는 ③단계에서 붙는다.

## 바뀐 점

| 항목 | 내용 |
| --- | --- |
| 방향 값 | `FrontLeft`, `FrontRight`, `BackLeft`, `BackRight`를 열거형 **끝에** 추가 (기존 4개의 순서·이름 그대로) |
| 반전 짝 | 우측면 → 좌측면, 앞 반측면(우) → (좌), 뒤 반측면(우) → (좌). `Source`·`IsMirrored`가 표로 계산 |
| 캐릭터별 방향 | `Character.HasThreeQuarter`, `StoredDirections`(3 또는 5), `Directions`(4 또는 8), `DirectionsChanged` |
| 파츠 뷰 | 반측면 뷰는 선택. 없으면 정면·후면 뷰로 대체(`Fallback`). `HasOwnView`, `PartView.Copy()` |
| 켜기·끄기 | `ThreeQuarterViews.Enable`(정면→앞 반측면, 후면→뒤 반측면 복사) / `Disable`(뷰·동작 반측면 키·손본 픽셀 삭제). 각각 되돌리기 한 번 |
| 반전 끊기 | `RightViewChange`에 방향 인자 추가 — 반측면(우)도 따로 그리기 가능 (기본값은 우측면, 기존 호출 그대로) |
| 동작 트랙 | 반측면 트랙은 선택. 비어 있으면 정면·후면 트랙을 재생 |
| 파일 | 반측면 뷰나 키·손본 픽셀이 있을 때만 형식 버전 2로 저장, 없으면 1 그대로. 반측면 트랙은 키가 있을 때만 씀 |
| 동작 가져오기 | 반측면 없는 캐릭터로 가져오면 반측면 키·손본 픽셀을 빼고, 앱에서 알려 줌 |
| 눈 파츠 | 반측면이 켜진 캐릭터에 눈을 추가하면 반측면 뷰도 만듦 (정면·후면 기준으로 시작) |

## 호환성 검증

| 검증 | 결과 |
| --- | --- |
| 기준 파일 테스트 (변경 **전** 코드로 기록: 레이어·각도 이미지·장착점·우측면 따로 그리기·눈 파츠·3방향 키·보간·손본 픽셀) | 저장 결과·불러오기→다시 저장·시트 PNG·JSON·GIF 모두 바이트 단위 동일 |
| 반측면 켰다 끄기 | 기준 파일과 바이트 단위 동일, 형식 버전 1 |
| 기준 파일 테스트가 변화를 잡는지 (형식 버전을 일부러 바꿔 봄) | 5개 실패로 잡힘 → 되돌림 |
| 앱 일괄 내보내기 (이전 버전으로 만든 `.dotchar` 3개) | 시트·JSON·GIF 27개 파일 모두 이전 내보내기와 바이트 단위 동일 |
| 앱 화면 (눈 파츠 추가·그리기·되돌리기·다시 실행) | 변경 전 스크린샷과 픽셀 단위 동일, 예외 없음 |
| 새 형식보다 높은 버전 파일 | "newer version" 오류로 거절 |

## 구조

| 위치 | 내용 |
| --- | --- |
| `Core/Rigging/Direction` | 새 값, `ThreeQuarterStored`·`ThreeQuarter`·`Every`, `Source`·`IsMirrored`·`IsThreeQuarter`·`Fallback`·`Key`, 라벨 |
| `Core/Rigging/Part`, `PartView` | 선택 뷰·대체, `HasOwnView`, `SetView`, `Copy` |
| `Core/Rigging/Character` | 반측면 포즈 2개 포함, `HasThreeQuarter`·`StoredDirections`·`Directions` |
| `Core/Rigging/ThreeQuarterViews` | 켜기·끄기·동작에서 빼기, 되돌리기 |
| `Core/Rigging/RightViewChange` | 반전 방향 일반화 (복사는 `PartView.Copy`로 옮김) |
| `Core/Rigging/CharacterTemplate` | `frontleft`·`backleft`(+`frontright`·`backright`) 있을 때만 읽고 씀 |
| `Core/Animation/AnimationClip`·`AnimationJson`·`ClipImport` | 선택 트랙·대체 재생·`HasThreeQuarterData`, 키 있는 반측면 트랙만 저장, 가져오기 처리 |
| `Core/Project/ProjectFile` | `FormatVersion` 2(읽는 최고), `ClassicFormatVersion` 1, `VersionFor` |
| 테스트 | 139개 통과 (새 13개: 기준 파일 3, 반측면 10) |

## 다음 단계

② 내보내기 방향 수 선택(시트·JSON·GIF·명령줄, 기본 4방향) → ③ 화면(방향 목록·미리보기·단축키·반측면 추가/지우기) → ④ 반측면 마네킹·기본 동작 트랙.
