namespace OrganizationHub.Web.Models;

public sealed class ServiceResult
{
    public bool IsPending { get; private init; }
    public string StatusMessage { get; private init; } = string.Empty;

    public static ServiceResult Pending(string statusMessage)
    {
        return new ServiceResult { IsPending = true, StatusMessage = statusMessage };
    }

    public bool IsConcurrencyConflict
    {
        get; private init;
    }
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

    public static ServiceResult Conflict(string errorMessage)
    {
        return new ServiceResult { IsSuccess = false, IsConcurrencyConflict = true, ErrorMessage = errorMessage };
    }
}

