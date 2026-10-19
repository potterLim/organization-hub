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


`workspace-ux.cjs`는 같은 격리 fixture의 `ux-admin`·`ux-member` 계정으로 PC 1440×1000·1920×1080과 모바일 480×1040을 검사합니다. 일정 초안 유지·명시적 폐기, 모바일 홈 3건 미리보기와 펼치기·접기, 직접 작성한 결재 제목과 독립적인 선택 파일·하위 저장 경로 요약, 파일명과 작업 버튼 배치, 업로드 권한별 빈 상태 안내 및 업로드 위치 전달을 검증합니다. 정상 데이터에는 홈에 표시할 할 일 12개와 `qa-team`의 분기 운영 계획 PDF가 있어야 합니다.

`HUB_TEST_URL`, `HUB_TEST_PASSWORD`, `HUB_SCREENSHOT_DIR` 및 필요한 외부 Playwright·Chromium 경로를 제공하고 `node tests/browser/workspace-ux.cjs`로 실행합니다. `/fixture/state/normal`·`empty`를 제공하는 테스트 호스트 전용 검사이며 운영 서버에는 실행하지 않습니다. 상태를 공유하므로 다른 fixture 상태 변경 검사와 순차 실행합니다. 파일 업로드나 결재 제출은 실행하지 않습니다.

날짜 팝업 검사는 좌우 경계뿐 아니라 상하 화면 경계와 고정 헤더 가림도 검사합니다. 날짜 선택·완료·재열기·Escape 및 트리거 포커스 복원도 함께 확인합니다.
