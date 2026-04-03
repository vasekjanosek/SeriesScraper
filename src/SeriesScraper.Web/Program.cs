using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SeriesScraper.Application.Security;
using SeriesScraper.Application.Services;
using SeriesScraper.Domain.Interfaces;
using SeriesScraper.Infrastructure.Data;
using SeriesScraper.Infrastructure.Repositories;
using SeriesScraper.Infrastructure.BackgroundServices;
using SeriesScraper.Infrastructure.Services;
using SeriesScraper.Infrastructure.Services.Imdb;
using SeriesScraper.Web.BackgroundServices;
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

    // Database
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Add services to the container.
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    builder.Services.AddHealthChecks();

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
    builder.Services.AddScoped<IForumRepository, ForumRepository>();

    // Scrape orchestration (#16)
    builder.Services.AddScoped<IScrapeOrchestrator, ScrapeOrchestrator>();
    builder.Services.AddScoped<IForumPostScraper, ForumPostScraper>();
    builder.Services.AddScoped<IForumSearchService, ForumSearchService>();
    builder.Services.AddScoped<ILinkExtractorService, LinkExtractorService>();
    builder.Services.AddScoped<ILinkRepository, LinkRepository>();
    builder.Services.AddScoped<ILinkTypeService, LinkTypeService>();
    builder.Services.AddScoped<ILinkTypeRepository, LinkTypeRepository>();

    // Singleton services
    builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();
    builder.Services.AddSingleton<IHtmlForumSectionParser, HtmlForumSectionParser>();
    builder.Services.AddSingleton<IResponseValidator, PhpBB2ResponseValidator>();

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

    // Watchlist service (#19)
    builder.Services.AddScoped<IWatchlistService, WatchlistService>();

    // History service (#33)
    builder.Services.AddScoped<IHistoryService, HistoryService>();
    builder.Services.AddScoped<IWatchlistRepository, WatchlistRepository>();

    // Run progress service (#32)
    builder.Services.AddScoped<IRunProgressService, RunProgressService>();
    builder.Services.AddScoped<IScrapeRunItemRepository, ScrapeRunItemRepository>();

    // Settings & App Info services (#34, #36)
    builder.Services.AddScoped<ISettingsService, SettingsService>();
    builder.Services.AddScoped<IAppInfoService, AppInfoService>();
    builder.Services.AddScoped<IDatabaseStatsProvider, DatabaseStatsProvider>();
    builder.Services.AddScoped<ISettingRepository, SettingRepository>();
    builder.Services.AddScoped<IDataSourceImportRunRepository, DataSourceImportRunRepository>();

    // Background services
    builder.Services.AddHostedService<ScrapeRunBackgroundService>();
    builder.Services.AddHostedService<ForumStructureRefreshService>();

    // Security services (#45, #46)
    builder.Services.AddSingleton<ISanitizer, HtmlContentSanitizer>();
    builder.Services.AddScoped<IUrlValidator, ForumUrlValidator>();

    var app = builder.Build();

    // Apply EF Core migrations on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

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

    app.MapHealthChecks("/healthz");
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
