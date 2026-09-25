# 체크포인트

`main`에 병합하지 않은 작업 브랜치의 되돌아갈 지점. 최신이 위.

| 이름 | 커밋 | 브랜치 | 내용 | 확인 |
| --- | --- | --- | --- | --- |
| `checkpoint-2026-09-25-three-quarter` | `ed4667c` | `claude/work-environment-setup-a3u93e` | 새로 만들기 동작 고르기, 눈 파츠·눈 위치, 반측면(3/4) ①~④와 동작 초안, 정리 패치노트 ([정리](patchnotes/2026-09-25-three-quarter-summary.md)) | 테스트 150개 통과, 빌드 오류 0 |

## 이 지점으로 돌아가기

```
git fetch origin claude/work-environment-setup-a3u93e
git checkout -b from-checkpoint ed4667c
```

이름(`checkpoint-...`)은 git 태그로 붙였지만, 작업 환경에서 태그를 원격에 올릴 수 없어 이 표의 커밋 번호가 기준이다. 커밋은 작업 브랜치에 올라가 있으므로 브랜치를 받으면 함께 받아진다.
