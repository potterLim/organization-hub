# Google 연동 가이드

Organization Hub는 하나의 운영 Google 계정을 연결해 Drive 폴더·Calendar·Google Tasks 목록을 사용합니다.

## OAuth 준비

Google Cloud Console에서 OAuth Client ID와 Client Secret을 만들고 앱 환경 변수 또는 secret 저장소에 설정합니다.

OAuth 동의 화면의 앱 이름은 제품명과 같은 `Organization Hub`로 설정합니다.

Google Cloud Console의 OAuth 클라이언트에는 Organization Hub 관리 화면의 `고급: 승인 주소 확인`에 표시되는 주소를 승인된 리디렉션 URI로 등록합니다.  
로컬 개발 기본 주소는 다음과 같습니다.

```text
http://127.0.0.1:5156/admin/google/callback
```

운영 배포에서는 실제 HTTPS 도메인의 `/admin/google/callback` 주소를 별도로 등록합니다.

필요한 권한 범위는 다음과 같습니다.

- Drive 파일과 폴더 조회·생성·수정·삭제
- Calendar 조회·생성·수정·삭제
- Tasks 조회·생성·수정·삭제
- 연결 계정 식별을 위한 `openid`, `email`

운영 Google 계정은 조직 자료를 보관하는 계정입니다. 개인 계정보다 조직이 관리하는 계정을 사용하는 것을 권장합니다.

## 연결

1. 시스템 관리자로 로그인합니다.
2. `/admin/google`로 이동합니다.
3. 계정 연결을 눌러 운영 Google 계정을 승인합니다.
4. 연결 진단에서 Drive 폴더·Calendar·Google Tasks 목록 상태를 확인합니다.

일반 사용자가 각자 Google 로그인을 할 필요는 없습니다. Organization Hub는 관리자가 연결한 운영 Google 계정을 통해 조직 자료를 관리합니다.

`/admin/google`은 Google 계정 연결과 표준 리소스 준비에 사용합니다.  
`/admin/settings`는 이미 연결된 Drive 폴더, 파일, Calendar, Google Tasks 목록을 운영 화면에 배정할 때 사용합니다.

## Drive 폴더 매핑

관리자는 Google 연결 화면에서 폴더를 선택해 다음 대상에 연결할 수 있습니다.

- 기본 자료 위치
- 관리팀 자료
- 외부 공유 자료
- 공통 자료
- 책임자 자료
- 업무 지원 자료
- 각 팀 자료
- 각 팀 결재 완료 자료

기본 폴더 만들기 기능은 연결된 Google 계정의 내 Drive 아래에 `Organization Hub` 기본 자료 위치를 만듭니다.  
공지 파일·업무 기준 파일·공통 자료·관리팀 자료·팀별 자료·책임자 자료·업무 지원 자료를 표준 구조로 생성하거나 재사용한 뒤 설정에 연결합니다.

기본 자료 위치가 지정되면 다른 Drive 폴더는 그 안에 있어야 합니다. 폴더를 새로 만들거나 바꾼 뒤에는 권한이 다른 사용자로 실제 조회와 업로드 가능 여부를 확인합니다.

시스템 설정의 Drive 탭에서는 탐색기를 열어 폴더나 파일을 직접 선택합니다.  
폴더는 자료 위치에 연결하고 파일은 공지 파일 또는 업무 기준 파일에 연결할 수 있습니다.

업무 기준 파일은 Drive에 보관하는 참고 자료입니다.  
사용자 화면의 업무 기준 카드는 별도 편집 화면에서 저장한 카드 데이터로 표시됩니다.

## Calendar와 Google Tasks 매핑

조직 공용·책임자·팀별 Calendar와 Google Tasks 목록을 연결할 수 있습니다.  
실제 조회와 수정 가능 여부는 사용자의 직책 권한과 연결된 Calendar 또는 Google Tasks 목록 기준으로 결정됩니다.

기본 Calendar 만들기 기능은 조직 공용·책임자·팀별 Calendar를 만들거나 재사용한 뒤 설정에 연결합니다.  
권한을 분리해야 하는 운영 영역은 서로 다른 Calendar에 연결하는 것을 권장합니다.

기본 Google Tasks 목록 만들기 기능은 연결된 Google 계정에서 필요한 Google Tasks 목록을 만들고 설정에 연결합니다.

Google Tasks 목록 이름은 `조직 공용 할 일`, `책임자 할 일`, `팀명 할 일` 형식으로 준비합니다.  
이미 운영 중인 목록이 있으면 표준 이름으로 정리한 뒤 설정에 연결합니다.

기본 Calendar를 만들 때 사용할 시간대는 `GoogleApi__CalendarTimeZone` 설정을 따릅니다. 별도로 지정하지 않으면 `Asia/Seoul`을 사용합니다.

할 일 마감 기한은 사용자의 브라우저 시간대를 기준으로 입력하고, Organization Hub는 이를 UTC 기준으로 보관합니다.  
화면에 다시 표시할 때는 현재 브라우저 시간대로 변환합니다.

Google Tasks의 `due` 값은 날짜 중심으로 보일 수 있어 시간까지 안정적으로 보여주는 용도로만 의존하지 않습니다.  
Organization Hub는 정확한 마감 시각을 앱 데이터와 함께 보존하고 Google Tasks 목록과 연결해 보여줍니다.

마감 시간을 정확히 유지하려면 할 일 생성과 수정은 Organization Hub 화면에서 처리합니다.  
Google Tasks 화면에서 직접 마감일을 바꾸면 Organization Hub에 표시되는 마감 시각과 다를 수 있습니다.

## 진단

Google 연결 진단은 다음을 확인합니다.

- Google 연결 설정과 승인 상태
- 필요한 Google 승인 범위 포함 여부
- Drive 폴더 존재 여부
- 폴더가 기본 자료 위치 안에 있는지
- Calendar가 연결 계정에 존재하는지
- Google Tasks 목록이 연결 계정에 존재하는지
- 설정된 공지 파일과 업무 기준 파일이 Drive에서 접근 가능한지

진단 오류가 있으면 먼저 Google 연결을 다시 확인한 뒤 Drive 폴더·Calendar·Google Tasks 매핑을 수정합니다.
