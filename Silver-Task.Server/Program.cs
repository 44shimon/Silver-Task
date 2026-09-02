using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Silver_Task.Server.Common;
using Silver_Task.Server.Common.Automation;
using Silver_Task.Server.Data;
using Silver_Task.Server.Hubs;
using Silver_Task.Server.Middleware;
using Silver_Task.Server.Models.Entities;
using Silver_Task.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF's Community license is free for organizations/individuals under $1M USD annual gross
// revenue (or non-profit/personal/educational use) — see ReportExportService's own doc comment.
// This project cannot verify Silver Group NY's revenue against that threshold; PDF export is
// implemented (it's the spec's own explicit requirement and the best technical fit) behind
// IReportExportService specifically so this is a contained, disclosed decision the business can
// revisit, not a silent dependency choice.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Add services to the container.

// The (sp, options) overload (rather than the plain options-only one this used before Phase 36)
// exists specifically so NotificationPushInterceptor can resolve IHubContext<NotificationHub>
// from DI — see that class's own doc comment for why a SaveChangesInterceptor, not a change to
// NotificationService.NotifyAsync, is the correct place to fire the real-time push.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.AddInterceptors(new NotificationPushInterceptor(sp.GetRequiredService<IHubContext<NotificationHub>>()));
});

const string FrontendCorsPolicy = "FrontendCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException(
        "Jwt:Secret is not configured. Set it via 'dotnet user-secrets set \"Jwt:Secret\" \"<value>\"' locally, or the Jwt__Secret environment variable in other environments.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // The token travels in an httpOnly cookie, not the Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AuthCookie.Name, out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Secure by default: any endpoint without an explicit [Authorize]/[AllowAnonymous]
    // requires authentication, so new controllers can't accidentally end up public.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Phase 59 — first-party, ships in the shared framework already (no new package). Only applied to
// AuthController.Login ([EnableRateLimiting("login")]) — this is on top of, not instead of,
// AuthService's existing per-account lockout (see AuthService.cs); the lockout stops repeated
// guesses against one account, this stops spraying many accounts from one IP fast enough to
// matter. Partitioned per client IP so one abusive source can't exhaust a shared global limit and
// lock out everyone else. Threshold is generous by design (default 10/60s) — well above any
// plausible legitimate login-retry pattern, well below what a credential-stuffing script wants.
var loginRateLimitPermits = builder.Configuration.GetValue("Security:LoginRateLimit:PermitLimit", 10);
var loginRateLimitWindowSeconds = builder.Configuration.GetValue("Security:LoginRateLimit:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = loginRateLimitPermits,
            Window = TimeSpan.FromSeconds(loginRateLimitWindowSeconds),
            QueueLimit = 0
        }));
});

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProjectAccessService, ProjectAccessService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ICustomFieldService, CustomFieldService>();
builder.Services.AddScoped<ICustomFieldValueValidator, CustomFieldValueValidator>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUserPreferencesService, UserPreferencesService>();
builder.Services.AddScoped<IUserNotificationSettingsService, UserNotificationSettingsService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITaskDependencyService, TaskDependencyService>();
builder.Services.AddScoped<IRecurringTaskService, RecurringTaskService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IFileCategoryService, FileCategoryService>();
builder.Services.AddScoped<IAutomationService, AutomationService>();
builder.Services.AddSingleton<IAutomationVariableResolver, AutomationVariableResolver>();
// Singleton, registered once and shared behind both interfaces — see AutomationDispatcher's own
// doc comment for why a single Channel-backed instance must back both the producer (services
// dispatching events) and consumer (the queue background service) sides.
builder.Services.AddSingleton<AutomationDispatcher>();
builder.Services.AddSingleton<IAutomationDispatcher>(sp => sp.GetRequiredService<AutomationDispatcher>());
builder.Services.AddSingleton<IAutomationEventQueue>(sp => sp.GetRequiredService<AutomationDispatcher>());
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<IEmailDeliveryService, EmailDeliveryService>();
builder.Services.AddScoped<IDigestGenerationService, DigestGenerationService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IReportExportService, ReportExportService>();
builder.Services.AddScoped<ISavedReportService, SavedReportService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ITemplateInstantiationService, TemplateInstantiationService>();
builder.Services.AddScoped<ISavedViewFilterEngine, SavedViewFilterEngine>();
builder.Services.AddScoped<ISavedViewService, SavedViewService>();
// Singleton — must be shared across every background service (themselves singletons via
// AddHostedService) and outlive per-request scopes. See IWorkerHeartbeatRegistry's own doc
// comment.
builder.Services.AddSingleton<IWorkerHeartbeatRegistry, WorkerHeartbeatRegistry>();
builder.Services.AddScoped<IDiagnosticsService, DiagnosticsService>();
builder.Services.AddHostedService<DueDateNotificationBackgroundService>();
builder.Services.AddHostedService<RecurringTaskGenerationBackgroundService>();
builder.Services.AddHostedService<AutomationQueueBackgroundService>();
builder.Services.AddHostedService<AutomationOverdueCheckBackgroundService>();
builder.Services.AddHostedService<NotificationRetentionBackgroundService>();
builder.Services.AddHostedService<EmailDeliveryBackgroundService>();
builder.Services.AddHostedService<DigestSchedulerBackgroundService>();

// First-party, ships in the shared framework already — no new server-side package. See
// NotificationHub's own doc comment for why the existing cookie-based JWT auth authorizes a hub
// connection with zero extra plumbing.
builder.Services.AddSignalR();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums (Status, Priority, Role, ...) travel over the wire as their string names
        // (e.g. "Administrator"), not numbers, to match the frontend's string literal types.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// One-off demo data population: `dotnet run -- --seed`. Deliberately gated to Development and
// never wired into the normal request pipeline — this is a manual dev-time tool, not a feature.
if (args.Contains("--seed"))
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("--seed is only allowed in the Development environment.");
    }

    await Silver_Task.Server.Data.Seeding.DemoDataSeeder.RunAsync(app.Services);
    return;
}

// Phase 48 — must be one of the first middlewares registered, before anything that reads
// Request.Scheme/Request.Host (UseHttpsRedirection right below it, in particular). Without this,
// running behind a reverse proxy that terminates TLS (the deployment topology this app is built
// for — see README "Production deployment checklist") means Kestrel only ever sees plain HTTP
// from the proxy, so UseHttpsRedirection would think every request needs redirecting even though
// the original client connection was already HTTPS. Defaults to trusting only loopback proxies
// (i.e. nginx/Caddy running on the same host as Kestrel); a reverse proxy on a different
// host/network must list its address under ForwardedHeaders:KnownProxies (see
// deploy/silvertask.env.example) or ForwardedHeadersMiddleware will discard the headers it sends.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
foreach (var proxy in app.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
{
    if (System.Net.IPAddress.TryParse(proxy, out var address))
    {
        forwardedHeadersOptions.KnownProxies.Add(address);
    }
}
app.UseForwardedHeaders(forwardedHeadersOptions);

// Phase 59 — registered before MaintenanceModeMiddleware specifically so even a 503 maintenance
// response (which short-circuits before reaching anything below it) still carries these headers —
// SecurityHeadersMiddleware only registers an OnStarting callback, it never itself short-circuits
// or blocks the request.
app.UseMiddleware<SecurityHeadersMiddleware>();

// Phase 54 — checked before literally everything else (including ExceptionHandlingMiddleware and
// the static SPA assets below), so a maintenance window enabled by scripts/update-debian.sh
// --activate blocks the whole app, not just authenticated API routes. See the middleware's own
// doc comment for why /api/health* is deliberately exempted.
app.UseMiddleware<MaintenanceModeMiddleware>();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseDefaultFiles();
// Phase 48 — AllowAnonymous is required here: MapStaticAssets registers as endpoint-routed
// endpoints (not classic static-file middleware), so without it the global FallbackPolicy
// (Program.cs above, "any endpoint without an explicit [Authorize]/[AllowAnonymous] requires
// auth") applies to the SPA's own JS/CSS bundle. That's invisible in dev (Vite's SpaProxy serves
// the SPA from a separate process entirely, never hitting this code path) and only surfaces in a
// real published build — caught during Phase 48's production-build verification, where every
// static asset request was returning 401, which would have completely locked every user out of
// even loading the login page.
app.MapStaticAssets().AllowAnonymous();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Phase 59 — HSTS is never sent in Development (a dev instance is almost always plain HTTP over
// localhost; browsers cache the HSTS directive and would then force HTTPS even for that localhost
// origin, breaking the dev workflow). Matches the standard ASP.NET Core template's own gating.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Same reasoning as MapStaticAssets above — the SPA shell itself must be loadable
// unauthenticated (the client-side router shows the login page when there's no session; the
// server can't require a session just to hand back the HTML shell that asks for one).
app.MapFallbackToFile("/index.html").AllowAnonymous();

app.Run();
