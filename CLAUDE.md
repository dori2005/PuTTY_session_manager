# PuTTY Session Manager — 에이전트 가이드

이 문서를 읽는 에이전트는 이 프로젝트를 이어받아 작업할 수 있다.

---

## 프로젝트 개요

PuTTY 세션을 그룹으로 묶어 관리하는 Windows 데스크탑 유틸리티.
기존 PSMP(PuTTY Session Manager Pro)의 대체품으로, **X 버튼 = 진짜 종료**가 핵심 차이점.

---

## 기술 스택

| 항목 | 내용 |
|------|------|
| 언어 | C# (.NET 10) |
| UI | Windows Forms |
| 데이터 저장 | `%APPDATA%\PuttySessionManager\groups.json` |
| 레지스트리 읽기 | `Microsoft.Win32.Registry` |
| JSON | `System.Text.Json` (내장) |

---

## 프로젝트 구조

```
PuttySessionManager/
├── PuttySessionManager.csproj
├── Program.cs                    # 진입점, 단일 인스턴스 Mutex
├── Models/
│   ├── PuttySession.cs           # 레지스트리에서 읽은 세션 (RegistryName + DisplayName)
│   └── SessionGroup.cs           # 그룹 모델 + AppData JSON 루트
├── Services/
│   ├── RegistryService.cs        # HKCU\Software\SimonTatham\PuTTY\Sessions 읽기
│   ├── GroupStorageService.cs    # groups.json 저장/불러오기
│   └── PuttyLaunchService.cs     # putty.exe 탐색 + Process.Start
└── Forms/
    ├── MainForm.cs                # 메인 UI 로직 (이벤트 핸들러)
    ├── MainForm.Designer.cs       # 컨트롤 초기화 (InitializeComponent)
    └── GroupEditDialog.cs         # 그룹 이름 입력 다이얼로그
```

---

## 핵심 설계 원칙

### X 버튼 = 진짜 종료
- `FormClosing`을 오버라이드하지 않는다
- `NotifyIcon` 코드가 없다
- WinForms 기본 동작: X → `Form.Close()` → `Application.Exit()`

### PuTTY 세션 읽기
- 레지스트리 키: `HKCU\Software\SimonTatham\PuTTY\Sessions`
- 각 서브키 이름 = URL 인코딩된 세션 이름
- `RegistryName`: 인코딩 그대로 (예: `My%20Server`) — putty.exe `-load` 인자로 사용
- `DisplayName`: `Uri.UnescapeDataString()`으로 디코딩 (예: `My Server`) — UI 표시용
- `Default%20Settings`는 제외

### 그룹 데이터 영속화
- 저장 위치: `%APPDATA%\PuttySessionManager\groups.json`
- `SessionGroup.SessionNames` = `RegistryName` 목록
- 앱 재시작 시 존재하지 않는 세션 이름은 표시에서 자동 제외 (데이터는 보존)

### PuTTY 실행
- `putty.exe -load "DisplayName"` 형식
- 탐색 순서: `PATH` 환경변수 → 일반 설치 경로(`Program Files\PuTTY`)

---

## 빌드 방법

**.NET 8 SDK가 필요하다.** SDK가 없다면 [dotnet.microsoft.com](https://dotnet.microsoft.com/download)에서 설치.

```cmd
# 개발 실행
dotnet run

# 단일 exe 배포 (framework-dependent, 작은 크기)
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=false

# 단일 exe 배포 (self-contained, .NET 10 런타임 포함)
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true
```

Visual Studio 2022로 열 경우: `.sln` 없이 폴더로 열거나, `.csproj`를 직접 열면 됨.

---

## 현재 상태 (2026-05-07)

- [x] 프로젝트 초기 구조 작성 완료
- [x] 핵심 기능 구현:
  - PuTTY 레지스트리 세션 읽기
  - 그룹 생성/삭제/이름변경
  - 세션 → 그룹 할당/해제
  - 더블클릭으로 PuTTY 실행
  - X 버튼 = 즉시 종료
  - 그룹 데이터 JSON 영속화
- [ ] 빌드 테스트 (PuTTY 설치 후 확인 필요)
- [ ] 아이콘 커스터마이징 (현재 SystemIcons 사용)

---

## 향후 개선 가능성

- 드래그 앤 드롭으로 세션을 그룹에 이동
- PuTTY 경로를 UI에서 수동 설정
- 그룹 일괄 연결 (그룹 내 모든 세션 한 번에 열기)
- 세션 검색/필터
- 다크 모드

---

## 주의사항

- `app.manifest` 파일이 없으면 `.csproj`에서 `<ApplicationManifest>app.manifest</ApplicationManifest>` 항목을 제거해야 빌드됨
- PuTTY가 미설치 상태면 세션 목록은 비어 있음 (에러 아님)
