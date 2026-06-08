using System.Text;
using Asp.Versioning;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using irp_pet.Auth;
using irp_pet.Background;
using irp_pet.Data;
using irp_pet.Messaging;
using irp_pet.Services;

namespace irp_pet.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIrpCore(this IServiceCollection services, IConfiguration config)
    {
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "IRP API",
                Version = "v1 & v2",
                Description = "v1 — полный API. v2 — /api/v2/alerts и /api/v2/incidents (пагинация)."
            });
            c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Description = "JWT из /auth/login. Вставь только токен (без слова Bearer).",
                Name = "Authorization",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Description = "API Key для POST /alerts (seed: dev-api-key-change-me)",
                Name = "X-Api-Key",
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey
            });
            c.DocumentFilter<SwaggerSecurityDocumentFilter>();
            // Один Swagger-док: v1 и v2 (ApiExplorer группирует по v1/v2, без этого v2 не попадает в UI).
            c.DocInclusionPredicate((_, _) => true);
        });

        services.AddApiVersioning(o =>
        {
            o.DefaultApiVersion = new ApiVersion(1, 0);
            o.AssumeDefaultVersionWhenUnspecified = true;
            o.ReportApiVersions = true;
            o.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(o =>
        {
            o.GroupNameFormat = "'v'VVV";
            o.SubstituteApiVersionInUrl = true;
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Application.ValidationBehavior<,>));
        services.AddAutoMapper(typeof(Program).Assembly);

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        var redisConn = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
            services.AddStackExchangeRedisCache(o => o.Configuration = redisConn);
        else
            services.AddDistributedMemoryCache();
        services.AddSingleton<RedisCacheService>();

        services.AddSingleton<JwtTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AlertService>();
        services.AddScoped<IncidentService>();
        services.AddScoped<AuditService>();
        services.AddScoped<OutboxService>();
        services.AddScoped<OnCallService>();
        services.AddSingleton<TelegramNotificationPolicy>();
        services.AddScoped<IncidentEventProcessor>();

        // Без Polly resilience: long-polling getUpdates (25s) несовместим с retry/timeout handler.
        services.AddHttpClient<TelegramApiClient>(c => c.Timeout = TimeSpan.FromSeconds(35));
        services.AddScoped<TelegramBotHandler>();
        services.AddScoped<INotificationService, TelegramNotificationService>();
        services.AddHttpClient<IJiraService, JiraService>()
            .AddStandardResilienceHandler();

        return services;
    }

    public static IServiceCollection AddIrpHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        var checks = services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");
        var redisConn = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
            checks.AddRedis(redisConn, name: "redis", failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);
        return services;
    }

    public static IServiceCollection AddIrpMessaging(this IServiceCollection services, IConfiguration config)
    {
        var rabbitEnabled = config.GetValue("RabbitMq:Enabled", false);
        if (rabbitEnabled)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<IncidentEventConsumer>();
                x.UsingRabbitMq((ctx, cfg) =>
                {
                    var rabbitHost = config["RabbitMq:Host"] ?? "localhost";
                    if (rabbitHost.Contains(':', StringComparison.Ordinal))
                    {
                        cfg.Host(new Uri($"rabbitmq://{rabbitHost}/"), h =>
                        {
                            h.Username(config["RabbitMq:Username"] ?? "guest");
                            h.Password(config["RabbitMq:Password"] ?? "guest");
                        });
                    }
                    else
                    {
                        cfg.Host(rabbitHost, "/", h =>
                        {
                            h.Username(config["RabbitMq:Username"] ?? "guest");
                            h.Password(config["RabbitMq:Password"] ?? "guest");
                        });
                    }
                    cfg.ConfigureEndpoints(ctx);
                });
            });
        }

        services.AddHostedService<OutboxDispatcherWorker>();
        return services;
    }

    public static IServiceCollection AddIrpBackgroundWorkers(this IServiceCollection services) =>
        services
            .AddHostedService<EscalationWorker>()
            .AddHostedService<TelegramBotWorker>();

    public static IServiceCollection AddIrpObservability(this IServiceCollection services, IConfiguration config)
    {
        var otlp = config["OpenTelemetry:OtlpEndpoint"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("irp_pet"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();
                if (!string.IsNullOrEmpty(otlp))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
            })
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddMeter(IrpMetrics.MeterName)
                .AddPrometheusExporter());
        return services;
    }

    public static IServiceCollection AddIrpAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var jwtIssuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer missing");
        var jwtAudience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience missing");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { });

        services.AddAuthorization();
        return services;
    }
}
