module Program

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open System
open Microsoft.AspNetCore.Mvc.ApplicationParts
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open System.IO
open System.Reflection
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
            .UseRouting()
            .UseStaticFiles()
        |> ignore

        app.MapDefaultControllerRoute() |> ignore

        app

app.Run()
