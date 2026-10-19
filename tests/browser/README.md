# 브라우저 회귀 검사

`approval-history-filters.cjs`는 결재 기록의 상태 선택 후 전체 기록 초기화, 진행 중 요청 전환, URL·탭 제목·모바일 폭·브라우저 오류를 검사합니다. 앱과 Google 연동을 격리한 테스트 환경 및 결재 기록 접근 권한이 있는 테스트 계정을 사용합니다.

Node.js와 Playwright 및 Chromium이 준비된 환경에서 다음 변수를 지정한 뒤 실행합니다. 비밀번호는 테스트 실행 환경의 비밀 변수로 전달하고 코드에 저장하지 않습니다.

```powershell
$env:HUB_TEST_URL = 'http://127.0.0.1:5164'
$env:HUB_TEST_USERNAME = 'test-user'
# HUB_TEST_PASSWORD: 테스트 계정 비밀번호를 환경에서 제공
node tests/browser/approval-history-filters.cjs
```

Playwright가 프로젝트 외부에 설치되어 있으면 `PLAYWRIGHT_MODULE`에 해당 모듈의 절대 경로를 지정할 수 있습니다. `PLAYWRIGHT_EXECUTABLE`로 이미 설치된 Chromium 실행 파일도 지정할 수 있습니다. 스크린샷은 임시 디렉터리에 저장되며 경로가 실행 결과에 표시됩니다. .NET 회귀 검사는 별도로 `dotnet test OrganizationHub.sln -c Release`로 실행합니다.
