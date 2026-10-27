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

`user-directory-layout.cjs`는 사용자 검색 결과가 없거나 팀·활성 상태 필터 조합이 빈 목록을 만들 때 안내 문구와 셀이 카드 및 실제 화면 폭 안에 들어오는지 검사합니다. 검색 해제 시 목록 복원, 아이디의 대소문자 구분 없는 검색, 표시 이름 검색, 필터 복원 및 기존 사용자 편집 폼도 확인하며 데이터를 저장하지 않습니다.

PC 1440×1000·1920×1080과 모바일 480×1040·360×800 및 모바일 표 변환 경계인 720·721px을 검사합니다. `HUB_TEST_URL`, `HUB_TEST_USER`, `HUB_TEST_PASSWORD`, `HUB_SCREENSHOT_DIR`를 제공하고 `node tests/browser/user-directory-layout.cjs`로 실행합니다. `PLAYWRIGHT_MODULE_PATH`와 `HUB_BROWSER_EXECUTABLE`로 외부 런타임을 지정할 수 있습니다. `HUB_MANAGER_USER`를 지정하면 같은 검사를 조직 관리자 계정에서도 실행하며, `HUB_TEST_WIDTHS=480,360`처럼 실행할 폭을 제한할 수 있습니다. `ux-member`(표시 이름 `이민준`), `경영지원팀`, `서비스기획팀`을 포함한 격리된 테스트 fixture가 필요합니다. 결과 JSON과 화면 원본은 지정한 외부 폴더에 저장됩니다.

`collaboration-ux.cjs`는 세 화면 크기와 10개 역할 계정에서 홈·일정·할 일·자료실·결재함을 확인합니다. 미저장 일정·할 일의 메뉴 이동 차단과 명시적 폐기, 조회 전용 계정의 긴 제목 표시, 사용자 관리의 첫 Tab 이동, 방향키 정렬 및 초점 유지, 로그인 후 요청 주소 복원과 외부 주소 차단을 검증합니다. 격리 fixture의 첫 공용 할 일 제목을 수정하며 각 화면 크기를 시작할 때 Google 모의 리소스를 초기화합니다. 환경 변수는 위 검사와 같고 `HUB_TEST_WIDTHS=480`으로 특정 폭만 실행할 수 있습니다.

`stale-delete.cjs`는 수정 전용 계정과 삭제 전용 계정의 독립 로그인으로 일정·할 일 삭제 경합을 세 화면 크기에서 재현합니다. 오래된 확인창에서는 삭제가 차단되고 최신 내용이 표시되어야 합니다. fixture는 DELETE의 If-Match 불일치를 HTTP 412로 반환하고, Tasks move 응답에 실제 작업 ID와 새 순서를 반환해야 합니다. 화면 원본·검사 결과는 `HUB_SCREENSHOT_DIR`에 저장합니다.


## 저장·권한·안내 문구 회귀 검사 (2026-10-27)

`Fixture/Fixture.csproj`는 실제 앱의 서비스 등록, 마이그레이션, 인증, 화면과 Google Calendar/Tasks SDK를 실행하는 로컬 전용 호스트입니다. Google HTTP 전송과 Drive 저장소에만 합성 테스트 대역을 사용합니다. 운영 Google 계정에 연결하거나 실제 Google 서버의 가용성을 확인하는 검사는 아닙니다. `seed.sql`과 PDF도 저장소에 포함되어 외부 캡처 호스트 없이 재현할 수 있습니다.

먼저 테스트 전용 `HUB_TEST_PASSWORD`를 환경 변수로 제공합니다. 첫 번째 PowerShell에서 저장소 루트를 기준으로 다음을 실행합니다. DB 경로는 매 실행마다 새 경로여야 하며 기존 파일이면 실행을 거부합니다. 호스트는 루프백 주소로만 열 수 있습니다.

```powershell
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('hub-fixture-' + [guid]::NewGuid())
$env:HUB_FIXTURE_ACK = 'ISOLATED_ONLY'
$env:HUB_APP_CONTENT_ROOT = Join-Path $fixtureRoot 'app'
$env:HUB_FIXTURE_DATABASE = Join-Path $fixtureRoot 'fixture.db'
dotnet publish src/OrganizationHub.Web -c Release -o $env:HUB_APP_CONTENT_ROOT
dotnet run --project tests/browser/Fixture --no-launch-profile -- --urls http://127.0.0.1:5202
```

두 번째 PowerShell에서 같은 테스트 비밀번호와 아래 변수를 지정합니다.

```powershell
$env:HUB_TEST_URL = 'http://127.0.0.1:5202'
$env:HUB_SCREENSHOT_DIR = Join-Path ([System.IO.Path]::GetTempPath()) ('hub-proof-' + [guid]::NewGuid())
node tests/browser/user-outcomes.cjs
```

외부 Playwright는 `PLAYWRIGHT_MODULE_PATH`, Chromium은 `HUB_BROWSER_EXECUTABLE`로 지정합니다. `user-outcomes.cjs`는 PC 1440×1000·1920×1080과 모바일 480×1040 DPR 3에서 일정 저장 거절과 초안 유지, 저장 후 경고 해제, 사용자 관리의 초안과 초점 복귀, 마감 시간의 로컬 저장 실패·복구, 업로드 기록 실패·정확히 한 번 복구, 삭제 응답 유실과 삭제 기록 실패, 소속 팀·다른 팀 위임 조회 및 자료 다운로드 경계, 승인본 준비 상태를 검사합니다. 변경한 상태만 PNG로 저장하고 `results.json`에 결과를 남깁니다. 모바일은 터치 화면 에뮬레이션이며 실기기 검사가 아닙니다.

기본 계정은 `ux-admin`, `ux-member`, `p1-create`, `p1-update`, `p1-delete`, `team-lead`, `final-approver`, `no-access`, `org-manager`, `new-member`, `operations-reader`, `planning-reader`, `delegated-reader`입니다. 모든 계정은 환경 변수로 받은 테스트 비밀번호를 사용합니다. `/fixture/*`는 합성 데이터의 상태·실패 주입·복구를 제어하는 전용 경로이며 운영 앱에는 등록되지 않습니다. 상태가 공유되므로 브라우저 시나리오는 순차 실행합니다.

Google 동작 기준은 [Tasks 리소스의 날짜 전용 마감과 삭제 플래그](https://developers.google.com/workspace/tasks/reference/rest/v1/tasks), [Calendar 조건부 수정·삭제](https://developers.google.com/calendar/api/guides/version-resources), [Calendar 410·412 오류](https://developers.google.com/workspace/calendar/api/guides/errors)를 확인했습니다. 테스트는 실제 SDK의 HTTP 직렬화·ETag 헤더·날짜 절삭·오류 변환을 통과하도록 구성했습니다.
