using Azure.Identity;
using BlazorAdmin;
using BlazorAdmin.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.DotNet.Scaffolding.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopWeb.Infrastructure;
using Microsoft.eShopWeb.Infrastructure.Data;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.eShopWeb.Web.HealthChecks;

namespace Microsoft.eShopWeb.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDatabaseContexts(this IServiceCollection services, IWebHostEnvironment environment, ConfigurationManager configuration)
    {
        // Allow in-memory DB even in Production if explicitly requested
        var useOnlyInMemory = string.Equals(configuration["UseOnlyInMemoryDatabase"], "true", StringComparison.OrdinalIgnoreCase);

        if (environment.IsDevelopment() || environment.IsDocker() || useOnlyInMemory)
        {
            // Local/dev (or learning mode on Azure) → use EF InMemory / local config
            services.ConfigureLocalDatabaseContexts(configuration);
            return;
        }

        // Production path with SQL/Key Vault (only if actually configured)
        var kvEndpoint = configuration["AZURE_KEY_VAULT_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(kvEndpoint))
        {
            var credential = new ChainedTokenCredential(new AzureDeveloperCliCredential(), new DefaultAzureCredential());
            configuration.AddAzureKeyVault(new Uri(kvEndpoint), credential);
        }

        services.AddDbContext<CatalogContext>((provider, options) =>
        {
            var catalogKey = configuration["AZURE_SQL_CATALOG_CONNECTION_STRING_KEY"];
            var connectionString = string.IsNullOrWhiteSpace(catalogKey) ? null : configuration[catalogKey];

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Missing Catalog DB connection string. Set AZURE_SQL_CATALOG_CONNECTION_STRING_KEY and its value (or enable UseOnlyInMemoryDatabase).");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                   .AddInterceptors(provider.GetRequiredService<DbCallCountingInterceptor>());
        });

        services.AddDbContext<AppIdentityDbContext>((provider, options) =>
        {
            var identityKey = configuration["AZURE_SQL_IDENTITY_CONNECTION_STRING_KEY"];
            var connectionString = string.IsNullOrWhiteSpace(identityKey) ? null : configuration[identityKey];

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Missing Identity DB connection string. Set AZURE_SQL_IDENTITY_CONNECTION_STRING_KEY and its value (or enable UseOnlyInMemoryDatabase).");

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                   .AddInterceptors(provider.GetRequiredService<DbCallCountingInterceptor>());
        });
    }

    public static void AddCookieAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });
    }

    public static void AddCustomHealthChecks(this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<ApiHealthCheck>("api_health_check", tags: new[] { "apiHealthCheck" })
            .AddCheck<HomePageHealthCheck>("home_page_health_check", tags: new[] { "homePageHealthCheck" });
    }

    public static void AddBlazor(this IServiceCollection services, ConfigurationManager configuration)
    {
        var configSection = configuration.GetRequiredSection(BaseUrlConfiguration.CONFIG_NAME);
        services.Configure<BaseUrlConfiguration>(configSection);

        // Blazor Admin Required Services for Prerendering
        services.AddScoped<HttpClient>(s => new HttpClient
        {
            BaseAddress = new Uri("https+http://blazoradmin")
        });

        // add blazor services
        services.AddBlazoredLocalStorage();
        services.AddServerSideBlazor();
        services.AddScoped<ToastService>();
        services.AddScoped<HttpService>();
        services.AddBlazorServices();
    }
}
