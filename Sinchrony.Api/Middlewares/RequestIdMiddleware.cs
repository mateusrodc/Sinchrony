namespace Sinchrony.Api.Middlewares;

public class RequestIdMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        context.Response.Headers["X-Request-ID"] = Guid.NewGuid().ToString();
        await next(context);
    }
}