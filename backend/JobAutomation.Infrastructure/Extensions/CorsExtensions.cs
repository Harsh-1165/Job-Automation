using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.DependencyInjection;

using Microsoft.Extensions.Hosting;



namespace JobAutomation.Infrastructure.Extensions;



public static class CorsExtensions

{

    public const string FrontendPolicy = "Frontend";



    public static IServiceCollection AddFrontendCors(

        this IServiceCollection services,

        IConfiguration configuration,

        IHostEnvironment environment)

    {

        var configuredOrigins = configuration["CORS_ALLOWED_ORIGINS"]

            ?? configuration["Cors:AllowedOrigins"];



        string[] origins;

        if (!string.IsNullOrWhiteSpace(configuredOrigins))

        {

            origins = configuredOrigins

                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        }

        else if (environment.IsDevelopment()
                 || environment.IsEnvironment("Testing")
                 || environment.IsEnvironment("RateLimitTesting"))

        {

            origins =

            [

                "http://localhost:3000",

                "http://127.0.0.1:3000"

            ];

        }

        else

        {

            throw new InvalidOperationException(

                "CORS_ALLOWED_ORIGINS must be configured in non-development environments.");

        }



        services.AddCors(options =>

        {

            options.AddPolicy(FrontendPolicy, policy =>

            {

                policy.WithOrigins(origins)

                    .AllowAnyHeader()

                    .AllowAnyMethod();

            });

        });



        return services;

    }

}

