namespace OrganizationHub.Web.Models;

public sealed class ServiceResult
{
    public bool IsSuccess
    {
        get; private init;
    }
    public string ErrorMessage { get; private init; } = string.Empty;

    private ServiceResult()
    {
    }

    public static ServiceResult Succeed()
    {
        return new ServiceResult
        {
            IsSuccess = true
        };
    }

    public static ServiceResult Fail(string errorMessage)
    {
        return new ServiceResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

