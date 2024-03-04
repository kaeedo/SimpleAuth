namespace Web.Controllers

open System
open System.Security.Claims
open System.Text.Json
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Web.Models

[<Authorize>]
type SecureController() =
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

        this.View(model)
