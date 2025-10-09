namespace x402dev.Web
{
    public class SecurityHeaderMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeaderMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Response.OnStarting(ctx =>
            {
                var headers = ((HttpContext)ctx).Response.Headers;

                headers.Remove("Server");

                headers.Remove("X-XSS-Protection");
                headers.Remove("X-Content-Type-Options");
                headers.Remove("X-Frame-Options");
                headers.Remove("Content-Security-Policy");

                headers.Add("X-XSS-Protection", "1; mode=block");
                headers.Add("X-Content-Type-Options", "nosniff");
                headers.Add("X-Frame-Options", "SAMEORIGIN");
                headers.Add("Content-Security-Policy", "frame-ancestors 'self'");

                return Task.CompletedTask;
            }, context);

            await _next(context);
        }
    }
}
