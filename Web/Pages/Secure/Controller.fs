namespace Web.Pages.Secure

open System
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

        let email =
            user.Claims
            |> Seq.find (fun c -> c.Type = "email")
            |> _.Value

        let metadata =
            user.Claims
            |> Seq.find (fun c -> c.Type = "user_metadata")
            |> _.Value

        let username =
            (JsonSerializer.Deserialize<{| username: string |}>(metadata))
                .username

        let model = {
            SecureModel.Email = email
            Username = username
        }

        this.View(model)
