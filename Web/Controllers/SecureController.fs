namespace Web.Controllers

open System
open System.Security.Claims
open System.Text.Json
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Routing
open Web.Models

[<Authorize>]
type SecureController(ctxAccessor: IHttpContextAccessor, linkGenerator: LinkGenerator) =
    inherit Controller()

    [<HttpGet>]
    member this.Index() =
        let user = this.HttpContext.User

        let userId =
            user.Claims
            |> Seq.find (fun c ->
                c.Type = "email"
                || c.Type = ClaimTypes.NameIdentifier)
            |> _.Value

        let username =
            match
                user.Claims
                |> Seq.tryFind (fun c -> c.Type = "user_metadata")
            with
            | Some c ->
                JsonSerializer
                    .Deserialize<{| username: string |}>(c.Value)
                    .username
            | None ->
                user.Claims
                |> Seq.find (fun c -> c.Type = ClaimTypes.Name)
                |> _.Value

        let model = {
            SecureModel.UserId = userId
            Username = username
        }

        let url = linkGenerator.GetUriByAction(ctxAccessor.HttpContext, "Index", "Secure")
        this.Response.Headers.Add("HX-Push-Url", url)
        this.View(model)
