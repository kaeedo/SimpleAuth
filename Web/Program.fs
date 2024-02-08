module Program

open System
open Microsoft.AspNetCore.Hosting

open Microsoft.AspNetCore.Builder

let configureServices (builder: WebApplicationBuilder) = builder

let app =
    Environment.GetCommandLineArgs()
    |> WebApplication.CreateBuilder
    |> fun builder -> builder.WebHost.UseUrls("https://0.0.0.0:5001")
    |> configureServices
    |> fun builder -> builder.Build()
    |> fun app ->
        app
            .UseHttpsRedirection()
            .UseRouting()
            .UseStaticFiles()
        |> ignore

        app

app.Run()
