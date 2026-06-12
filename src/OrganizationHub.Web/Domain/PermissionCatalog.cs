using System.Collections.Generic;
using OrganizationHub.Web.Models;

namespace OrganizationHub.Web.Domain;

public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionDefinition> GetDefinitions()
    {
        List<PermissionDefinition> definitions = new List<PermissionDefinition>();
        addAdministrationDefinitions(definitions);
        addMenuDefinitions(definitions);
        addFileDefinitions(definitions);
        addCalendarDefinitions(definitions);
        addTodoDefinitions(definitions);
        addApprovalDefinitions(definitions);
        return definitions;
    }

    private static void addAdministrationDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.SYSTEM_SETTINGS_MANAGE, "관리", "시스템 설정 관리", "서비스 전역 설정을 관리합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.ORGANIZATION_MANAGE, "관리", "조직 관리", "팀과 조직 단위를 만들고 수정합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.POSITIONS_MANAGE, "관리", "직책 관리", "팀별 직책을 만들고 수정합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.PERMISSIONS_MANAGE, "관리", "권한 관리", "직책별 권한 범위를 설정합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.USERS_MANAGE, "관리", "사용자 관리", "계정, 소속, 직책을 관리합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.GOOGLE_CONNECTION_MANAGE, "관리", "Google 연결 관리", "조직 운영용 Google 계정 연결을 관리합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.MENUS_MANAGE, "관리", "메뉴 관리", "메뉴 노출과 접근 권한을 관리합니다."));
    }

    private static void addMenuDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.MENU_READ, "메뉴", "메뉴 열람", "허용된 메뉴에 접근합니다."));
    }

    private static void addFileDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.FILES_READ, "자료", "자료 열람", "자료실의 파일과 폴더를 열람합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.FILES_UPLOAD, "자료", "자료 업로드", "자료실에 파일과 폴더를 추가합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.FILES_UPDATE, "자료", "자료 수정", "자료실 항목의 이름과 메타데이터를 수정합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.FILES_DELETE, "자료", "자료 삭제", "자료실 항목을 삭제합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.FILES_APPROVED_STORE, "자료", "승인본 저장", "결재 완료 자료실에 승인본을 저장합니다."));
    }

    private static void addCalendarDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.CALENDAR_READ, "일정", "일정 열람", "캘린더 일정을 열람합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.CALENDAR_CREATE, "일정", "일정 생성", "캘린더 일정을 만듭니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.CALENDAR_UPDATE, "일정", "일정 수정", "캘린더 일정을 수정합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.CALENDAR_DELETE, "일정", "일정 삭제", "캘린더 일정을 삭제합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.CALENDAR_AUDIT_READ, "일정", "일정 삭제 기록 열람", "삭제된 일정 기록을 열람합니다."));
    }

    private static void addTodoDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.TODOS_READ, "할 일", "할 일 열람", "할 일 목록을 열람합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.TODOS_CREATE, "할 일", "할 일 생성", "할 일을 만듭니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.TODOS_UPDATE, "할 일", "할 일 수정", "할 일을 수정하거나 완료 처리합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.TODOS_DELETE, "할 일", "할 일 삭제", "할 일을 삭제합니다."));
    }

    private static void addApprovalDefinitions(List<PermissionDefinition> definitions)
    {
        definitions.Add(new PermissionDefinition(PermissionKeys.APPROVAL_REQUEST, "결재", "결재 요청", "자료 결재를 요청합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.APPROVAL_APPROVE, "결재", "결재 승인", "결재 단계의 승인자로 참여합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.APPROVAL_FINAL_APPROVE, "결재", "최종 승인", "결재의 최종 승인자로 참여합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.APPROVAL_HISTORY_READ, "결재", "결재 기록 열람", "결재 이력과 승인 상태를 열람합니다."));
        definitions.Add(new PermissionDefinition(PermissionKeys.APPROVAL_WORKFLOWS_MANAGE, "결재", "결재 경로 관리", "조건별 결재 경로와 승인 단계를 관리합니다."));
    }
}
