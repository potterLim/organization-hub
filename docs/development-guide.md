# 유지보수 가이드

이 문서는 Organization Hub를 로컬에서 실행하고 배포 전 검증하는 방법을 설명합니다.

## 요구 사항

- .NET SDK 10
- Node.js
- Git

## 로컬 실행

```powershell
dotnet run --project src\OrganizationHub.Web\OrganizationHub.Web.csproj --urls http://127.0.0.1:5156
```

개발 환경 기본 계정은 다음과 같습니다.

```text
admin / ChangeMe!2026
```

운영 환경에서는 반드시 초기 시스템 관리자 계정을 환경 변수로 지정합니다.

## Google OAuth 설정

개발 환경에서는 user secrets를 사용합니다.

```powershell
dotnet user-secrets set "GoogleApi:OAuthClientID" "<client-id>" --project src\OrganizationHub.Web\OrganizationHub.Web.csproj
dotnet user-secrets set "GoogleApi:OAuthClientSecret" "<client-secret>" --project src\OrganizationHub.Web\OrganizationHub.Web.csproj
```

로컬 OAuth 리디렉션 URI는 `http://127.0.0.1:5156/admin/google/callback`입니다. Visual Studio 실행 프로필과 명령줄 실행 주소를 이 값과 맞춰야 Google 연결이 완료됩니다.

운영 환경에서는 환경 변수나 배포 플랫폼의 secret 저장소를 사용합니다. OAuth Client Secret, 운영 DB, Data Protection key는 저장소에 커밋하지 않습니다.

## 검증

```powershell
dotnet build OrganizationHub.sln
dotnet test OrganizationHub.sln
dotnet format OrganizationHub.sln --verify-no-changes --verbosity minimal
```

권한, 메뉴, Google 매핑, 결재 경로를 바꾼 경우에는 관리자 화면에서 최소한 다음 흐름을 확인합니다.

1. 시스템 관리자로 팀, 직책, 권한, 사용자를 만든다.
2. 권한이 다른 사용자로 로그인해 보이는 메뉴와 가능한 작업을 비교한다.
3. 권한을 변경한 뒤 새로고침 없이 메뉴와 접근 가능 여부가 갱신되는지 확인한다.
4. 파일, 일정, 할 일, 결재 요청처럼 서버 요청이 발생하는 작업을 직접 실행한다.

## 데이터베이스 마이그레이션

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> --project src\OrganizationHub.Web\OrganizationHub.Web.csproj --startup-project src\OrganizationHub.Web\OrganizationHub.Web.csproj --output-dir Data\Migrations
```

운영 데이터베이스에 영향을 주는 변경은 백업, 마이그레이션 검토, 로컬 검증 순서로 진행합니다.

## 로컬 산출물

브라우저 캡처, 임시 QA 데이터, 로컬 스크립트, 대량 테스트 로그는 `tmp`, `captures`, `tools/local`, `tools/tmp` 같은 무시되는 경로에 둡니다. 배포 코드와 문서에 필요한 파일만 Git에 포함합니다.
