# 기술 개요

Organization Hub는 ASP.NET Core Blazor Server 기반의 내부 운영 포털입니다.  
배포자는 데이터 위치·Google 연동·캐시 동작·단일 인스턴스 전제를 이해하고 운영합니다.

## 기술 스택

- ASP.NET Core Blazor Server
- EF Core와 SQLite
- MudBlazor
- Google Drive API
- Google Calendar API
- Google Tasks API

로컬 실행과 검증 명령은 [유지보수 가이드](development-guide.md)에서 다룹니다.

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

운영 프로세스는 Blazor Server UI·MVC 컨트롤러·EF Core·Google API 서비스로 구성됩니다.

## 데이터

운영에 필요한 설정과 기록은 SQLite와 EF Core Migration으로 관리합니다.

주요 데이터는 다음과 같습니다.

- 사용자와 팀
- 팀별 직책
- 직책별 권한 부여
- 결재 워크플로와 결재 단계
- Google Drive 폴더와 파일 ID
- Calendar ID와 화면 표시용 Calendar 이름
- Google Tasks 목록 ID
- 메뉴 섹션과 메뉴 표시 설정
- 업무 기준 카드
- 업로드 기록, 일정 삭제 기록, 결재 감사 기록
- 일정과 할 일 작성자·마감 기한 보조 데이터

Google Drive 파일 원본과 Calendar 원본 및 Google Tasks 원본은 연결된 Google 계정에 남습니다.

파일 업로드와 결재 완료 파일 복사는 같은 이름의 파일이 있을 때 기존 파일 교체와 새 파일 추가를 모두 지원합니다.  
새 파일 추가는 `파일명 (2).확장자` 형식으로 사용 가능한 이름을 계산합니다.

## 권한 처리

권한 판단은 서버 서비스 계층에서 수행합니다.  
사용자가 활성 직책을 갖고 있으면 직책의 권한 부여가 우선 적용됩니다.

메뉴 표시·파일 요청·일정 변경·할 일 변경·결재 작업은 각각 서버에서 다시 권한을 확인합니다.  
화면에서 버튼이 보이지 않는 것만으로 보안을 판단하지 않습니다.

## 결재

결재 문서 내용은 `ApprovalDocuments`에 보존하고 `ApprovalRequests.DocumentHash`로 무결성을 검사합니다. 목록 조회는 문서 BLOB을 읽지 않으며, 관련자 또는 기록 열람 권한이 있는 사용자의 별도 다운로드 요청에서만 보존본을 읽습니다.

`ApprovalRequests.Revision`은 낙관적 동시성 토큰입니다. 동시에 승인·반려가 들어오면 한 상태 전이만 저장됩니다. 최종 결재는 발행 작업(`ApprovalPublications`)과 함께 `Publishing` 상태로 저장하고, 발행 완료 확인 뒤 `Approved`로 바뀝니다.

새 승인본은 Drive 사전 발급 ID를 사용합니다. 동일 작업의 재시도는 ID·파일명·대상 위치·보존 내용을 변경하지 않으며, Drive appProperties의 작업 식별자와 MD5 전송 체크섬으로 이미 저장된 파일을 확인합니다. 로컬 보존본의 무결성은 SHA-256으로 검사합니다. 기존 파일 교체 작업도 같은 저장 영수증을 사용하고, 같은 대상 파일의 미완료 발행 작업은 DB 유일 인덱스로 중복 예약을 막습니다.

발행 작업에는 2분의 처리 임대와 90초 작업 제한을 둡니다. 백그라운드 작업자가 30초마다 복구 대상을 확인하며 실패한 작업은 1분 뒤 다시 시도합니다. 프로세스가 중단되면 임대가 만료된 뒤 같은 작업을 재개합니다. Google과 SQLite 사이에 분산 트랜잭션을 제공하는 것은 아니며, 명시적인 중간 상태와 영속 작업 기록으로 완료 여부를 조정합니다.

결재는 관리자가 만든 워크플로와 단계 설정을 사용합니다.

워크플로는 요청 팀 조건과 요청 직책 조건으로 적용 대상을 정합니다.  
여러 워크플로가 동시에 맞으면 팀과 직책 조건이 모두 맞는 경로를 먼저 사용하고, 그다음 팀 조건·직책 조건·전체 조건 순서로 확인합니다.

워크플로 단계는 특정 사용자·특정 직책·요청자 팀의 직책·지정 팀의 직책·권한 보유자 기준으로 결재자를 찾습니다.  
배포 후에는 조직의 대표 결재 흐름을 실제 계정으로 확인합니다.

## Google 연동

Google 연동은 서비스 계층에서 캡슐화합니다.

- `GoogleDriveService`
- `GoogleCalendarService`
- `GoogleTasksTodoService`
- `GoogleWorkspaceSetupService`
- `GoogleHealthCheckService`

운영자는 Google 연결 화면의 진단 결과를 기준으로 Google 승인 범위·Drive 폴더·Calendar·Google Tasks 목록 연결 상태를 확인합니다.

시스템 설정 화면은 Drive 폴더와 파일, Calendar, Google Tasks 목록을 운영 화면에 배정합니다.  
Drive 파일 배정은 공지 파일과 업무 기준 파일에 사용하며, 업무 기준 카드 자체는 별도 데이터로 관리합니다.

Calendar는 운영 영역에 연결된 Google Calendar를 기준으로 조회하고 수정합니다.  
일정에는 화면 표시용 Calendar 이름과 작성자 정보를 안정적으로 보여주기 위한 보조 데이터를 함께 저장합니다.

할 일 마감 기한은 UTC 기준 값으로 저장하고 사용자 화면에서는 브라우저 시간대 기준으로 변환합니다.  
Google Tasks API의 `due` 값은 날짜 중심으로 동작해 시간 정보가 안정적으로 보존되지 않습니다.

Organization Hub는 Google Tasks에는 할 일 원본과 날짜 정보를 저장하고, 정확한 마감 시각은 앱 데이터에 별도로 보존합니다.  
따라서 Organization Hub에서 만든 할 일은 사용자의 브라우저 시간대에 맞춰 같은 마감 시각으로 표시됩니다.

고급 탭에서는 ID를 직접 확인하거나 수정할 수 있지만 일반 운영 흐름에서는 목록 선택 방식을 사용합니다.

## 캐시와 변경 감지

Google API 호출 비용을 줄이기 위해 메모리 캐시를 사용합니다.  
변경 작업 후에는 관련 캐시를 무효화하고 변경 버전으로 로딩 중 변경 여부를 확인합니다.

현재 구조는 단일 서버 인스턴스 기준입니다.  
다중 인스턴스 배포가 필요하면 캐시와 변경 버전 저장소를 외부 공유 저장소로 분리해야 합니다.
