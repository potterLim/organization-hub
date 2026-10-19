using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace OrganizationHub.Web.Tests.Infrastructure;

internal sealed class StaticJavaScriptRuntime : IJSRuntime
{
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? argsOrNull)
        {
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? argsOrNull)
        {
            return InvokeAsync<TValue>(identifier, argsOrNull);
        }
    }
