# 배포 가이드

Organization Hub는 한 조직이 한 서버 인스턴스로 운영하는 구성을 전제로 합니다.

## 준비 사항

- Linux 서버 또는 컨테이너 실행 환경
- Docker와 Docker Compose
- 도메인과 HTTPS 인증서
- Google OAuth Client ID와 Client Secret
- 조직 운영용 Google 계정
- 운영 데이터 볼륨 백업 위치

## 환경 변수

초기 시스템 관리자와 Google OAuth 값은 운영 환경에서 반드시 지정합니다.

| 변수 | 설명 |
| --- | --- |
| `ORGANIZATION_HUB_SEED_USERNAME` | 최초 시스템 관리자 아이디 |
| `ORGANIZATION_HUB_SEED_PASSWORD` | 최초 시스템 관리자 비밀번호 |
| `ORGANIZATION_HUB_SEED_DISPLAY_NAME` | 최초 시스템 관리자 표시 이름 |
| `GOOGLE_OAUTH_CLIENT_ID` | Google OAuth Client ID |
| `GOOGLE_OAUTH_CLIENT_SECRET` | Google OAuth Client Secret |

초기 시스템 관리자 계정은 데이터베이스에 사용자가 하나도 없을 때만 생성됩니다. 운영을 시작한 뒤에는 관리자 화면에서 비밀번호를 변경하고 필요한 실제 운영 계정을 따로 만듭니다.

## 실행

```bash
docker compose up -d --build
docker compose ps
```

앱은 reverse proxy 뒤에서 HTTPS로 공개하는 것을 권장합니다. Blazor Server 연결을 위해 WebSocket upgrade 헤더가 유지되어야 합니다.

## 데이터 위치와 백업

SQLite 데이터베이스와 Data Protection key는 운영 데이터 볼륨에 저장합니다. 이 볼륨에는 사용자, 팀, 직책, 권한, 결재, Google 매핑, 감사 기록이 포함됩니다.

반드시 백업해야 하는 항목은 다음과 같습니다.

- 운영 데이터 볼륨
- 환경 변수 값
- reverse proxy 설정
- Google OAuth 설정

파일, Calendar, Google Tasks 원본은 연결된 Google 계정에 남습니다.

## 업데이트

운영 DB를 업데이트하기 전에는 데이터 볼륨을 백업합니다.

```bash
git pull
docker compose up -d --build
```

앱 시작 시 EF Core Migration이 적용됩니다. 업데이트 후에는 로그인, Google 연결 진단, 권한별 메뉴 표시, 파일/일정/할 일/결재 흐름을 확인합니다.

## 배포 점검

- HTTPS 로그인 화면이 열리는가
- 초기 시스템 관리자 계정으로 로그인되는가
- Google 계정 연결과 연결 진단이 정상인가
- 관리팀과 일반 팀을 만들고 각 팀의 직책 권한을 설정할 수 있는가
- Drive 폴더, Calendar, Google Tasks 목록을 연결할 수 있는가
- 권한 없는 사용자가 관리자 메뉴와 다른 팀 자료에 접근할 수 없는가
- 파일 업로드, PDF 보기, 일정, 할 일, 결재 요청이 정상 동작하는가
- 백업 복구 절차를 운영자가 알고 있는가
