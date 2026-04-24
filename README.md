# MyApp — WPF 자동업데이트 템플릿

OneDrive for Business를 업데이트 서버로 활용하는 WPF 앱 템플릿입니다.

## 기능

- 앱 시작 시 자동 버전 체크
- 수동 업데이트 확인 버튼
- 다운로드 진행률 표시
- SHA256 해시 검증
- 강제/선택 업데이트 지원
- single exe 및 zip 배포 지원
- 업데이트 실패 시 자동 롤백 (.bak 백업)

## 프로젝트 구조

```
MyApp/
├── MyApp.csproj           ← 프로젝트 설정 (버전, publish 설정)
├── App.xaml / App.xaml.cs
├── MainWindow.xaml/.cs    ← 메인 화면 (버전 표시, 업데이트 버튼)
├── UpdateDialog.xaml/.cs  ← 업데이트 다이얼로그 (진행률, 변경내용)
├── UpdateInfo.cs          ← update.json 모델
└── UpdateService.cs       ← 업데이트 핵심 로직
```

## 빌드 및 배포

### Single EXE 빌드

```bash
dotnet publish -c Release
```

출력 경로: `bin\publish\net10.0-windows\win-x64\MyApp.exe`

### 버전 변경

`MyApp.csproj`에서 수정:

```xml
<Version>1.2.0</Version>
<AssemblyVersion>1.2.0.0</AssemblyVersion>
<FileVersion>1.2.0.0</FileVersion>
```

## 자동업데이트 설정 가이드

### 1. OneDrive 폴더 준비

OneDrive for Business에 업데이트용 폴더를 만듭니다.

```
My Files/Apps/Wpf_template/
├── update.json
└── MyApp.exe (또는 MyApp.zip)
```

<img width="717" height="273" alt="image" src="https://github.com/user-attachments/assets/55f66a56-0651-45b5-a219-ca4e2887ec5f" />

<br><br>

### 2. update.json 작성

```json
{
  "version": "1.1.0",
  "fileName": "MyApp.exe",
  "shareId": "여기에_exe파일의_shareId",
  "hash": "sha256:여기에_해시값",
  "mandatory": false,
  "changelog": "변경 내용을 여기에 작성"
}
```

<img width="580" height="193" alt="image" src="https://github.com/user-attachments/assets/4184c253-e604-4232-a461-c619997aed73" />

<br><br>

| 필드 | 설명 |
|---|---|
| `version` | 새 버전 번호 (현재 앱보다 높아야 업데이트 감지) |
| `fileName` | 다운로드할 파일명 (`MyApp.exe` 또는 `MyApp_1.1.0.zip`) |
| `shareId` | exe/zip 파일의 OneDrive 공유 링크에서 추출한 ID |
| `hash` | `sha256:해시값` 형식. 비워두면 검증 생략 |
| `mandatory` | `true`면 강제 업데이트 (나중에 버튼 비활성화) |
| `changelog` | 업데이트 다이얼로그에 표시할 변경 내용 |

<br><br>

### 3. OneDrive 공유 링크 생성

각 파일(update.json, exe/zip)마다:

1. OneDrive에서 파일 우클릭 → **공유**
2. **"링크가 있는 모든 사용자"** 선택 (중요!)
3. 링크 복사

<img width="791" height="640" alt="image" src="https://github.com/user-attachments/assets/ed2ccbc2-2c32-4f78-b44a-785604da3b56" />

<br><br>

### 4. shareId 추출 방법

공유 링크 예시:
```
https://tenant-my.sharepoint.com/:u:/g/personal/user_tenant_onmicrosoft_com/IQBo2mgEO12oTpxVCCBwF3jcAa50fA9CiomljfHfsnHWMHg?e=JeAjun
```

`/g/personal/.../` 뒤의 문자열이 shareId:
```
IQBo2mgEO12oTpxVCCBwF3jcAa50fA9CiomljfHfsnHWMHg
```

`?e=` 이후는 제외합니다.

### 5. UpdateService.cs 수정

`UpdateService.cs` 상단의 상수를 본인 환경에 맞게 수정:

```csharp
private const string BaseUrl = "https://your-tenant-my.sharepoint.com/personal/your_user/_layouts/15/download.aspx?share=";
private const string UpdateJsonShareId = "update.json의_shareId";
```

### 6. SHA256 해시 생성

PowerShell:
```powershell
(Get-FileHash .\MyApp.exe -Algorithm SHA256).Hash.ToLower()
```

update.json에 `sha256:` 접두사와 함께 입력:
```json
"hash": "sha256:a1b2c3d4e5f6..."
```

해시 검증을 생략하려면 빈 문자열로 두면 됩니다:
```json
"hash": ""
```

## 새 버전 배포 절차

1. `MyApp.csproj`에서 버전 번호 올리기
2. `dotnet publish -c Release`
3. `bin\publish\net10.0-windows\win-x64\MyApp.exe`를 OneDrive에 업로드
4. 업로드한 파일의 공유 링크 생성 → shareId 추출
5. SHA256 해시 생성
6. `update.json` 수정 (version, shareId, hash, changelog)
7. `update.json` 재업로드 (기존 파일 덮어쓰기 — shareId 유지됨)

## 업데이트 동작 흐름

```
앱 시작 (또는 수동 버튼 클릭)
  → update.json 다운로드
  → 버전 비교 (현재 < 서버)
  → 업데이트 다이얼로그 표시
  → 사용자 확인 → 파일 다운로드 (진행률 표시)
  → SHA256 해시 검증
  → 배치 스크립트 생성 → 앱 종료
  → 기존 exe 백업 (.bak)
  → 새 파일로 교체
  → 앱 재시작 → 임시 파일 정리
```

## zip 배포 시

zip 안에 exe 및 관련 파일을 포함시키면 됩니다. 업데이트 시 zip을 압축 해제하여 앱 디렉토리에 덮어씁니다.

```json
{
  "version": "1.2.0",
  "fileName": "MyApp_1.2.0.zip",
  "shareId": "zip파일의_shareId",
  "hash": "sha256:zip파일의_해시값",
  "mandatory": false,
  "changelog": "zip 배포 테스트"
}
```

## 주의사항

- OneDrive 공유 링크는 반드시 **"링크가 있는 모든 사용자"**로 설정해야 합니다. 조직 내부 전용이면 인증이 필요하여 앱에서 403 오류가 발생합니다.
- update.json을 덮어쓰기로 재업로드하면 기존 shareId가 유지됩니다. 삭제 후 재업로드하면 shareId가 변경되므로 주의하세요.
- `mandatory: true`로 설정하면 사용자가 업데이트를 건너뛸 수 없습니다.
