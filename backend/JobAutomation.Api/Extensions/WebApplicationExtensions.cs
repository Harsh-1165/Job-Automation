using Hangfire;
using JobAutomation.Api.Middleware;
using JobAutomation.Infrastructure.Extensions;

namespace JobAutomation.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        app.UseMiddleware<RequestCorrelationMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        if (!app.Environment.IsDevelopment()
            && !app.Environment.IsEnvironment("Testing")
            && !app.Environment.IsEnvironment("RateLimitTesting"))
        {
            app.UseHsts();
        }

        app.UseCors(CorsExtensions.FrontendPolicy);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Job Automation API v1");
            });

            var jobStorage = app.Services.GetService<JobStorage>();
            if (jobStorage is not null)
            {
                app.UseHangfireDashboard("/hangfire", new DashboardOptions(), jobStorage);
            }
        }

        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}
