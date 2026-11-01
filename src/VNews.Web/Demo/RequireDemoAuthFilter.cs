using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace VNews.Web.Demo;

/// <summary>
/// Runs before anti-forgery validation (lower Order) and answers 404 whenever the demo sign-in is not allowed for the
/// request, so a disabled or production-like deployment reveals nothing, whatever the request contains.
/// </summary>
public sealed class RequireDemoAuthFilter : IAuthorizationFilter, IOrderedFilter
{
    private readonly DemoAuthGate _gate;
    private readonly ILogger<RequireDemoAuthFilter> _logger;

    public RequireDemoAuthFilter(DemoAuthGate gate, ILogger<RequireDemoAuthFilter> logger)
    {
        _gate = gate;
        _logger = logger;
    }

    public int Order => -1000;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var decision = _gate.Evaluate(context.HttpContext);
        if (decision == DemoAuthDecision.Available) return;

        // Quietly ignore the expected "not enabled" cases; flag the ones that look like probing or misconfiguration.
        var level = decision is DemoAuthDecision.NotDevelopment or DemoAuthDecision.Disabled ? LogLevel.Debug : LogLevel.Warning;
        _logger.Log(level, "Demo sign-in refused: {Decision} (database {Database}).", decision, _gate.DatabaseName);
        context.Result = new NotFoundResult();
    }
}
