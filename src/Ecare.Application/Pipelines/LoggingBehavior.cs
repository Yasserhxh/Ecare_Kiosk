using MediatR;
using Microsoft.Extensions.Logging;

namespace Ecare.Application.Pipelines;
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {Name} {@Request}", name, request);
        var response = await next();
        logger.LogInformation("Handled {Name}", name);
        return response;
    }
}
