# PixelAniMaker

4등신 도트 캐릭터를 파츠와 스켈레톤으로 조립하고, 보정된 애니메이션 스프라이트 시트로 내보내는 데스크톱 프로그램.
C# .NET 8 + Avalonia UI로 만들며 윈도우·맥·리눅스를 지원한다.

- 설계 문서: [doc/design.md](doc/design.md)
- 패치노트: [doc/patchnotes/](doc/patchnotes/README.md)
- 진행 상황과 다음 루트: [doc/progress.md](doc/progress.md)
- 프로토타입 스크립트: [mannequin/](mannequin/) (템플릿 생성, 팔 흔들기 회전 보정 테스트)

## 실행

.NET 8 SDK가 필요하다.

```bash
dotnet run --project src/PixelAniMaker.App
```

저장한 프로젝트 열기, 창 없이 시트·GIF 일괄 내보내기:

```bash
dotnet run --project src/PixelAniMaker.App -- my_character.dotchar
```

```bash
dotnet run --project src/PixelAniMaker.App -- --export my_character.dotchar --out export
```

## 배포 파일 만들기

설치 없이 실행되는 exe 하나를 `publish/win-x64/`에 만든다 (`-Runtime osx-arm64`, `linux-x64` 등도 가능).

```bash
powershell -ExecutionPolicy Bypass -File scripts/publish.ps1
```

## 테스트

```bash
dotnet test
```

## 구조

| 경로 | 역할 |
| --- | --- |
| `src/PixelAniMaker.Core` | UI와 무관한 로직: 인덱스 이미지, 팔레트, 도구, 되돌리기 |
| `src/PixelAniMaker.App` | Avalonia 앱: 도킹 창, 캔버스, 팔레트, 미리보기 |
| `tests/PixelAniMaker.Core.Tests` | Core 단위 테스트 |
