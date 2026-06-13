# 기술 개요

Organization Hub는 ASP.NET Core Blazor Server 기반의 내부 운영 포털입니다. 배포자는 데이터 위치, Google 연동, 캐시 동작, 단일 인스턴스 전제를 이해하고 운영합니다.

## 런타임 구성

```text
src/OrganizationHub.Web
├─ Components
├─ Controllers
├─ Data
├─ Domain
├─ Models
├─ Services
├─ ClientScripts
└─ wwwroot
```

운영 프로세스는 Blazor Server UI, MVC 컨트롤러, EF Core, Google API 서비스로 구성됩니다.

## 데이터

운영에 필요한 설정과 기록은 SQLite와 EF Core Migration으로 관리합니다.

주요 데이터는 다음과 같습니다.

- 사용자와 팀
- 팀별 직책
- 직책별 권한 부여
- 결재 워크플로와 결재 단계
- Google 폴더, 캘린더, 할 일 목록 ID
- 업로드 기록과 일정 삭제 기록

Google Drive 파일 원본, 캘린더 원본, 할 일 원본은 연결된 Google 계정에 남습니다.

## 권한 처리

권한 판단은 서버 서비스 계층에서 수행합니다. 사용자가 활성 직책을 갖고 있으면 직책의 권한 부여가 우선 적용됩니다.

메뉴 표시, 파일 요청, 일정 변경, 할 일 변경, 결재 작업은 각각 서버에서 다시 권한을 확인합니다. 화면에서 버튼이 보이지 않는 것만으로 보안을 판단하지 않습니다.

## 결재

결재는 관리자가 만든 워크플로와 단계 설정을 사용합니다.

워크플로 단계는 특정 사용자, 특정 직책, 요청자 팀의 직책, 지정 팀의 직책, 권한 보유자 기준으로 결재자를 찾습니다. 배포 후에는 조직의 대표 결재 흐름을 실제 계정으로 검증합니다.

## Google 연동

Google 연동은 서비스 계층에서 캡슐화합니다.

- `GoogleDriveService`
- `GoogleCalendarService`
- `GoogleTasksTodoService`
- `GoogleWorkspaceSetupService`
- `GoogleHealthCheckService`

운영자는 Google 연결 화면의 진단 결과를 기준으로 Google 승인 범위, 자료 폴더, 캘린더, 할 일 목록 연결 상태를 확인합니다.

## 캐시와 변경 감지

Google API 호출 비용을 줄이기 위해 메모리 캐시를 사용합니다. 변경 작업 후에는 관련 캐시를 무효화하고 변경 버전으로 로딩 중 변경 여부를 확인합니다.

현재 구조는 단일 서버 인스턴스 기준입니다. 다중 인스턴스 배포가 필요하면 캐시와 변경 버전 저장소를 외부 공유 저장소로 분리해야 합니다.
