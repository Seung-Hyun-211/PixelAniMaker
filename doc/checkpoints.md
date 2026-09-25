# 체크포인트

`main`에 병합하지 않은 작업 브랜치의 되돌아갈 지점. 최신이 위.

| 이름 | 커밋 | 브랜치 | 내용 | 확인 |
| --- | --- | --- | --- | --- |
| `checkpoint-2026-09-25-secondary` | `c428b35` | `claude/work-environment-setup-a3u93e` | 위 체크포인트 이후: 가슴 볼륨 파츠, 2차 모션(흔들림), 반측면 패치노트의 예시 캐릭터 이미지를 마네킹으로 교체 ([progress.md](progress.md) 3.5절 9·10) | 테스트 175개 통과, 빌드 오류 0 |
| `checkpoint-2026-09-25-features` | `41aa856` | `claude/work-environment-setup-a3u93e` | 위 체크포인트 이후: 프레임별 PNG 내보내기, 파츠 숨기기·잠금, 프레임별 길이, 회전 범위, 반측면 기본 동작 검토, 기록 창, 자동 명암, 설정 변경의 저장 표시 ([progress.md](progress.md) 3.5절) | 테스트 164개 통과, 빌드 오류 0 |
| `checkpoint-2026-09-25-three-quarter` | `ed4667c` | `claude/work-environment-setup-a3u93e` | 새로 만들기 동작 고르기, 눈 파츠·눈 위치, 반측면(3/4) ①~④와 동작 초안, 정리 패치노트 ([정리](patchnotes/2026-09-25-three-quarter-summary.md)) | 테스트 150개 통과, 빌드 오류 0 |

## 이 지점으로 돌아가기

```
git fetch origin claude/work-environment-setup-a3u93e
git checkout -b from-checkpoint c428b35   # 또는 41aa856, ed4667c (이전 체크포인트)
```

이름(`checkpoint-...`)은 git 태그로 붙였지만, 작업 환경에서 태그를 원격에 올릴 수 없어 이 표의 커밋 번호가 기준이다. 커밋은 작업 브랜치에 올라가 있으므로 브랜치를 받으면 함께 받아진다.
