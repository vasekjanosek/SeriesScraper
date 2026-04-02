using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Serilog;
using SeriesScraper.Application.Security;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;
using SeriesScraper.Infrastructure.Services;
using SeriesScraper.Infrastructure.Services.Imdb;
using SeriesScraper.Web.BackgroundServices;
using SeriesScraper.Web.Data;
using SeriesScraper.Web.Logging;

// Set global regex timeout as safety net for ReDoS prevention (#48)
SafeRegex.SetGlobalTimeout();

// Bootstrap logger for startup errors (before host is built)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json + environment variables
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Destructure.With<CredentialDestructuringPolicy>()
            .Enrich.FromLogContext();
    });

    // Add services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddSingleton<WeatherForecastService>();

    // Antiforgery for any HTTP POST endpoints (#47)
    builder.Services.AddAntiforgery();

    // Scraping job queue (singleton — shared between UI and BackgroundService)
    builder.Services.AddSingleton<IScrapingJobQueue, ScrapingJobQueue>();

    // Session management (scoped — one session manager per scope/circuit)
    builder.Services.AddScoped<IForumSessionManager, ForumSessionManager>();

    // Scoped services
    builder.Services.AddScoped<IScrapeRunService, ScrapeRunService>();
    builder.Services.AddScoped<IScrapeRunRepository, ScrapeRunRepository>();
    builder.Services.AddScoped<IForumSectionDiscoveryService, ForumSectionDiscoveryService>();
    builder.Services.AddScoped<IForumSectionRepository, ForumSectionRepository>();

    // Singleton services
    builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();
    builder.Services.AddSingleton<IHtmlForumSectionParser, HtmlForumSectionParser>();

    // IMDB matching engine
    builder.Services.AddSingleton<ITitleNormalizer, TitleNormalizer>();
    builder.Services.AddScoped<IMediaTitleRepository, MediaTitleRepository>();
    builder.Services.AddScoped<IMediaEpisodeRepository, MediaEpisodeRepository>();
    builder.Services.AddScoped<IMediaRatingRepository, MediaRatingRepository>();
    builder.Services.AddScoped<IImdbTitleDetailsRepository, ImdbTitleDetailsRepository>();
    builder.Services.AddScoped<IMetadataSource, ImdbMetadataSource>();
    builder.Services.AddScoped<IImdbMatchingService, ImdbMatchingService>();

    // Results service
    builder.Services.AddScoped<IResultsService, ResultsService>();
    builder.Services.AddScoped<IResultsQueryRepository, ResultsQueryRepository>();

    // Background service
    builder.Services.AddHostedService<ScrapeRunBackgroundService>();

    // Security services (#45, #46)
    builder.Services.AddSingleton<ISanitizer, HtmlContentSanitizer>();
    builder.Services.AddScoped<IUrlValidator, ForumUrlValidator>();

    var app = builder.Build();

    // Serilog request logging middleware
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseStaticFiles();

    app.UseRouting();

    // Antiforgery middleware for CSRF protection on any non-Blazor POST endpoints (#47)
    app.UseAntiforgery();

    app.MapBlazorHub();
    app.MapFallbackToPage("/_Host");

    Log.Information("SeriesScraper starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
