using System.Collections.Generic;

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
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.SYSTEM_SETTINGS_MANAGE),
            Category = "관리",
            DisplayName = "시스템 설정 관리",
            Description = "조직 전체에 적용되는 기본 설정을 관리합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.ORGANIZATION_MANAGE),
            Category = "관리",
            DisplayName = "조직 관리",
            Description = "팀과 조직 단위를 만들고 수정합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.POSITIONS_MANAGE),
            Category = "관리",
            DisplayName = "직책 만들기",
            Description = "팀별 직책을 만들고 수정합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.PERMISSIONS_MANAGE),
            Category = "관리",
            DisplayName = "직책 권한 설정",
            Description = "직책별 권한 범위를 설정합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.USERS_MANAGE),
            Category = "관리",
            DisplayName = "사용자 관리",
            Description = "계정, 소속, 직책을 관리합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.GOOGLE_CONNECTION_MANAGE),
            Category = "관리",
            DisplayName = "Google 연결 관리",
            Description = "조직 운영용 Google 계정 연결을 관리합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.MENUS_MANAGE),
            Category = "관리",
            DisplayName = "메뉴 보이기 설정",
            Description = "메뉴 표시 대상과 접근 권한을 관리합니다."
        });
    }

    private static void addMenuDefinitions(List<PermissionDefinition> definitions)
    {
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.MENU_READ),
            Category = "메뉴",
            DisplayName = "메뉴 열람",
            Description = "허용된 메뉴에 접근합니다."
        });
    }

    private static void addFileDefinitions(List<PermissionDefinition> definitions)
    {
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.FILES_READ),
            Category = "자료",
            DisplayName = "자료 열람",
            Description = "자료실의 파일과 폴더를 열람합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.FILES_UPLOAD),
            Category = "자료",
            DisplayName = "자료 업로드",
            Description = "자료실에 파일과 폴더를 추가합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.FILES_UPDATE),
            Category = "자료",
            DisplayName = "자료 수정",
            Description = "자료실 항목의 이름과 위치를 수정합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.FILES_DELETE),
            Category = "자료",
            DisplayName = "자료 삭제",
            Description = "자료실 항목을 삭제합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.FILES_APPROVED_STORE),
            Category = "자료",
            DisplayName = "승인본 저장",
            Description = "결재 완료 자료실에 승인본을 저장합니다."
        });
    }

    private static void addCalendarDefinitions(List<PermissionDefinition> definitions)
    {
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.CALENDAR_READ),
            Category = "일정",
            DisplayName = "일정 열람",
            Description = "캘린더 일정을 열람합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.CALENDAR_CREATE),
            Category = "일정",
            DisplayName = "일정 생성",
            Description = "캘린더 일정을 만듭니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.CALENDAR_UPDATE),
            Category = "일정",
            DisplayName = "일정 수정",
            Description = "캘린더 일정을 수정합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.CALENDAR_DELETE),
            Category = "일정",
            DisplayName = "일정 삭제",
            Description = "캘린더 일정을 삭제합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.CALENDAR_AUDIT_READ),
            Category = "일정",
            DisplayName = "일정 삭제 기록 열람",
            Description = "삭제된 일정 기록을 열람합니다."
        });
    }

    private static void addTodoDefinitions(List<PermissionDefinition> definitions)
    {
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.TODOS_READ),
            Category = "할 일",
            DisplayName = "할 일 열람",
            Description = "할 일 목록을 열람합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.TODOS_CREATE),
            Category = "할 일",
            DisplayName = "할 일 생성",
            Description = "할 일을 만듭니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.TODOS_UPDATE),
            Category = "할 일",
            DisplayName = "할 일 수정",
            Description = "할 일을 수정하거나 완료 처리합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.TODOS_DELETE),
            Category = "할 일",
            DisplayName = "할 일 삭제",
            Description = "할 일을 삭제합니다."
        });
    }

    private static void addApprovalDefinitions(List<PermissionDefinition> definitions)
    {
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.APPROVAL_REQUEST),
            Category = "결재",
            DisplayName = "결재 요청",
            Description = "자료 결재를 요청합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.APPROVAL_APPROVE),
            Category = "결재",
            DisplayName = "결재 승인",
            Description = "결재 단계의 승인자로 참여합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.APPROVAL_FINAL_APPROVE),
            Category = "결재",
            DisplayName = "최종 승인",
            Description = "결재의 최종 승인자로 참여합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.APPROVAL_HISTORY_READ),
            Category = "결재",
            DisplayName = "결재 기록 열람",
            Description = "결재 이력과 승인 상태를 열람합니다."
        });
        addDefinition(definitions, new PermissionDefinitionSeed
        {
            Key = new PermissionKey(PermissionKeys.APPROVAL_WORKFLOWS_MANAGE),
            Category = "결재",
            DisplayName = "결재 경로 설정",
            Description = "조건별 결재 경로와 승인 단계를 관리합니다."
        });
    }

    private static PermissionDefinition createDefinition(PermissionDefinitionSeed definitionSeed)
    {
        return new PermissionDefinition
        {
            Key = definitionSeed.Key.GetRequiredValue(),
            Category = definitionSeed.Category,
            DisplayName = definitionSeed.DisplayName,
            Description = definitionSeed.Description
        };
    }

    private static void addDefinition(List<PermissionDefinition> definitions, PermissionDefinitionSeed definitionSeed)
    {
        PermissionDefinition definition = createDefinition(definitionSeed);
        definitions.Add(definition);
    }
}
