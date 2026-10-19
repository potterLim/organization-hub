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

`popup-conflict-layout.cjs`는 PC 1440×1000·1920×1080, 모바일 480×1040·360×800에서 일정 시작·종료, 할 일 생성·수정의 날짜 팝업과 파일명 단일·다중 중복 안내를 검사합니다. 팝업은 모바일 넘침으로 함께 커질 수 있는 `innerWidth` 대신 `documentElement.clientWidth`와 비교합니다. 안내문의 읽기 가능한 폭, 영역 간 겹침, 버튼의 패널 이탈과 교체 가능 여부도 확인합니다.

실행 환경은 2026년 10월의 `task1`, `qa-team` 폴더의 동일한 이름 파일 두 개를 포함한 격리된 캡처 fixture를 사용합니다. `HUB_TEST_URL`, `HUB_TEST_USER`, `HUB_TEST_PASSWORD`, `HUB_SCREENSHOT_DIR`를 제공한 뒤 `node tests/browser/popup-conflict-layout.cjs`로 실행합니다. 외부 Playwright와 Chromium 경로는 `PLAYWRIGHT_MODULE_PATH`, `HUB_BROWSER_EXECUTABLE`로 지정합니다. 화면 원본과 검사 결과 JSON은 `HUB_SCREENSHOT_DIR`에 저장됩니다.

`HUB_LAYOUT_AREA=popup` 또는 `conflict`로 영역별 실행도 가능합니다. `HUB_APPROVER_USER`를 제공하면 결재 승인본의 단일·다중 중복 안내도 검사합니다. 이 경우 fixture의 `/fixture/state/normal`, `/fixture/state/approval-multiple`이 같은 문서명의 승인본을 각각 한 개·두 개 반환하고, 최종 결재자에게 `신규 고객 온보딩 프로세스 개선안` 요청이 배정되어 있어야 합니다. 테스트는 중복 확인까지만 진행하며 승인본 교체·추가를 실행하지 않습니다.
