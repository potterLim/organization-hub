# Google 연동 가이드

Organization Hub는 하나의 운영 Google 계정을 연결해 Drive, Calendar, Tasks를 사용합니다.

## OAuth 준비

Google Cloud에서 OAuth Client ID와 Client Secret을 만들고 앱 환경 변수 또는 secret 저장소에 설정합니다.

OAuth 동의 화면의 앱 이름은 제품명과 같은 `Organization Hub`로 설정합니다.

Google Cloud Console의 OAuth 클라이언트에는 Organization Hub 관리 화면에 표시되는 `OAuth 콜백 URI`를 승인된 리디렉션 URI로 등록합니다. 로컬 개발 기본 주소는 다음과 같습니다.

```text
http://127.0.0.1:5156/admin/google/callback
```

운영 배포에서는 실제 HTTPS 도메인의 `/admin/google/callback` 주소를 별도로 등록합니다.

필요한 권한 범위는 다음과 같습니다.

- Drive 파일과 폴더 조회, 생성, 수정
- Calendar 조회, 생성, 수정, 삭제
- Tasks 조회, 생성, 수정, 삭제

운영 Google 계정은 조직 자료를 보관하는 계정입니다. 개인 계정보다 조직이 관리하는 계정을 사용하는 것을 권장합니다.

## 연결

1. 시스템 관리자로 로그인합니다.
2. `/admin/google`로 이동합니다.
3. 계정 연결을 눌러 운영 Google 계정을 승인합니다.
4. 연결 진단에서 Drive, Calendar, Tasks 상태를 확인합니다.

일반 사용자가 각자 Google 로그인을 할 필요는 없습니다. 제품은 관리자가 연결한 운영 Google 계정을 통해 조직 자료를 관리합니다.

## Drive 매핑

관리자는 Google 연결 화면에서 폴더를 선택해 다음 대상에 연결할 수 있습니다.

- 공식 루트
- 관리팀 자료
- 외부 공유 자료
- 공통 자료
- 책임자 자료
- 업무 지원 자료
- 각 팀 자료
- 각 팀 결재 완료 자료

기본 폴더 만들기 기능은 연결된 Google 계정의 My Drive 아래에 `Organization Hub` 루트를 만들고, 공통 자료, 관리팀 자료, 팀별 자료, 책임자 자료, 업무 지원 자료를 표준 구조로 생성하거나 재사용한 뒤 설정에 연결합니다.

공식 루트가 지정되면 다른 자료 폴더는 공식 루트 아래에 있어야 합니다. 폴더를 새로 만들거나 바꾼 뒤에는 권한이 다른 사용자로 실제 조회와 업로드 가능 여부를 확인합니다.

## Calendar와 Tasks 매핑

조직 공용, 책임자, 팀별 Calendar와 Tasks 목록을 연결할 수 있습니다. 실제 조회와 수정 가능 여부는 사용자의 직책 권한으로 결정됩니다.

기본 Calendar 만들기 기능은 조직 공용, 책임자, 팀별 Calendar를 만들거나 재사용한 뒤 설정에 연결합니다.

기본 할 일 목록 만들기 기능은 연결된 Google 계정에서 필요한 Tasks 목록을 만들고 설정에 연결합니다.

## 진단

Google 연결 진단은 다음을 확인합니다.

- OAuth 연결 상태
- 필요한 scope 포함 여부
- Drive 폴더 존재 여부
- 폴더가 공식 루트 아래에 있는지
- Calendar ID가 연결 계정에 존재하는지
- Tasks 목록 ID가 연결 계정에 존재하는지

진단 오류가 있으면 먼저 Google 연결을 다시 확인하고, 그 다음 폴더/캘린더/목록 매핑을 수정합니다.
