module Program

open System.Collections.Generic
open Fido2NetLib
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Routing
open System
open Microsoft.AspNetCore.Mvc.ApplicationParts
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open System.IO
open System.Reflection
open Microsoft.Extensions.Options
open Web.Services
open Westwind.AspNetCore.LiveReload
open Microsoft.AspNetCore.Authentication.Cookies

let registerCompiledViewsAssembly (mvcBuilder: IMvcBuilder) =
    let currentAssembly = Assembly.GetExecutingAssembly()
    let folderPath = Path.GetDirectoryName(currentAssembly.Location)

    let assemblyPath =
        Path.Combine(folderPath, currentAssembly.GetName().Name + ".Views.dll")

    let viewsAssembly = Assembly.LoadFrom(assemblyPath)
    let viewsApplicationPart = CompiledRazorAssemblyPart(viewsAssembly)

    mvcBuilder.ConfigureApplicationPartManager(fun manager -> manager.ApplicationParts.Add(viewsApplicationPart))
    |> ignore

    ()

let app =
    Environment.GetCommandLineArgs()
    |> WebApplication.CreateBuilder
    |> fun builder ->
        builder.WebHost.UseUrls([| "https://0.0.0.0:5001" |])
        |> ignore

        builder
    |> fun builder ->
        builder.Services.Configure<RouteOptions>(fun (options: RouteOptions) -> options.LowercaseUrls <- true)
        |> ignore

        builder.Services.AddHttpContextAccessor()
        |> ignore

        builder.Configuration
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile("appsettings.local.json", true, true)
        |> ignore

        builder.Services
            .AddAuthentication(fun authenticationOptions ->
                authenticationOptions.DefaultAuthenticateScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                authenticationOptions.DefaultChallengeScheme <- CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(fun options ->
                options.LoginPath <- "/authentication/signin"
                options.LogoutPath <- "/authentication/signout")
        |> ignore

        builder.Services.AddAuthorization() |> ignore

        ////////////////////
        // These three services are key to getting auth with Supabase working
        ////////////////////
        builder.Services.AddScoped<Supabase.Client>(fun sp ->
            let url = builder.Configuration["Supabase:BaseUrl"]

            let key = builder.Configuration["Supabase:SecretApiKey"]

            Supabase.Client(url, key))
        |> ignore

        builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>()
        |> ignore

        builder.Services.AddScoped<SupabaseAuthService>()
        |> ignore
        ////////////////////

        ////////////////////
        // Passwordless auth service
        ////////////////////
        builder.Services.AddTransient<PasswordlessService>()
        |> ignore
        ////////////////////

        builder.Services.AddSingleton<FakeDatabase>()
        |> ignore

        builder.Services.AddSingleton<PasskeyFakeDatabase>()
        |> ignore

        builder.Services.AddSession(fun options ->
            // Set a short timeout for easy testing.
            options.IdleTimeout <- TimeSpan.FromMinutes(2)
            options.Cookie.HttpOnly <- true
            // Strict SameSite mode is required because the default mode used
            // by ASP.NET Core 3 isn't understood by the Conformance Tool
            // and breaks conformance testing
            options.Cookie.SameSite <- SameSiteMode.Unspecified)
        |> ignore

        builder.Services.Configure<Fido2Configuration>(fun (options: Fido2Configuration) ->
            options.ServerDomain <- builder.Configuration["fido2:serverDomain"]
            options.ServerName <- "FIDO2 Test"

            options.Origins <-
                builder.Configuration
                    .GetSection("fido2:origins")
                    .Get<HashSet<string>>()

            options.TimestampDriftTolerance <- builder.Configuration.GetValue<int>("fido2:timestampDriftTolerance")
            options.MDSCacheDirPath <- builder.Configuration["fido2:MDSCacheDirPath"]

            options.BackupEligibleCredentialPolicy <-
                builder.Configuration.GetValue<Fido2Configuration.CredentialBackupPolicy>(
                    "fido2:backupEligibleCredentialPolicy"
                )

            options.BackedUpCredentialPolicy <-
                builder.Configuration.GetValue<Fido2Configuration.CredentialBackupPolicy>(
                    "fido2:backedUpCredentialPolicy"
                )

            ())
        |> ignore

        builder.Services.AddSingleton<Fido2Configuration>(fun resolver ->
            resolver
                .GetRequiredService<IOptions<Fido2Configuration>>()
                .Value)
        |> ignore

        builder.Services.AddScoped<IFido2, Fido2>()
        |> ignore

        // builder.Services.AddSingleton<IMetadataService, NullMetadataService>()
        // |> ignore

        // This registers a bunch of services we need for Razor
        let mvcBuilder = builder.Services.AddControllersWithViews()

        // tell Razor Engine where to look for compiled assemblies
        registerCompiledViewsAssembly mvcBuilder
#if DEBUG
        builder.Services.AddLiveReload() |> ignore
        // only do runtime compilation when developing
        mvcBuilder.AddRazorRuntimeCompilation() |> ignore
#endif
        builder.Services.AddRazorPages() |> ignore
        builder
    |> _.Build()
    |> fun app ->
#if DEBUG
        app.UseLiveReload() |> ignore
#endif
        app
            .UseHttpMethodOverride(
                let o = HttpMethodOverrideOptions()
                o.FormFieldName <- "X-Http-Method-Override"
                o
            )
            .UseHttpsRedirection()
            .UseForwardedHeaders()
            .UseSession()
            .UseRouting()
            .UseAuthentication()
            .UseAuthorization()
            .UseStaticFiles()
        |> ignore

        app.MapDefaultControllerRoute() |> ignore

        app

app.Run()
