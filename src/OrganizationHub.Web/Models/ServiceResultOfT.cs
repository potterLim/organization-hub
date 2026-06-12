namespace OrganizationHub.Web.Models;

public sealed class ServiceResultOfT<TValue>
{
    public bool IsSuccess
    {
        get; private init;
    }
    public string ErrorMessage { get; private init; } = string.Empty;
    public TValue? ValueOrNull
    {
        get; private init;
    }

    private ServiceResultOfT()
    {
    }

    public static ServiceResultOfT<TValue> Succeed(TValue value)
    {
        return new ServiceResultOfT<TValue>
        {
            IsSuccess = true,
            ValueOrNull = value
        };
    }

    public static ServiceResultOfT<TValue> Fail(string errorMessage)
    {
        return new ServiceResultOfT<TValue>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

