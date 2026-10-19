using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace OrganizationHub.Web.Services;

internal static class ResourceEditVersion
{
    public static void Apply(HttpRequestMessage message, string etag)
    {
        Validate(etag);
        message.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
    }

    public static void Validate(string etag)
    {
        if (EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? parsedOrNull) == false || parsedOrNull.Tag == "*" || parsedOrNull.IsWeak)
        {
            throw new ResourceEditConflictException();
        }
    }

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> executeAsync)
    {
        try
        {
            return await executeAsync();
        }
        catch (Google.GoogleApiException exception) when (exception.HttpStatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new ResourceEditConflictException();
        }
    }
}
