namespace Web.Controllers

open System
open System.Security.Claims
open System.Text.Json
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.Routing
open Web.Models

type SecureControllerError =
    | UserNotFound
    | EmailClaimMissing

[<Authorize>]
type SecureController(ctxAccessor: IHttpContextAccessor, linkGenerator: LinkGenerator) =
    inherit Controller()

    [<HttpGet>]
    member this.Index() : IActionResult =
        let model: Result<SecureModel, SecureControllerError> =
            result {
                let! user =
                    this.HttpContext.User
                    |> Result.requireNotNull SecureControllerError.UserNotFound

                let! userId =
                    user.Claims
                    |> Seq.tryFind (fun c ->
                        c.Type = "email"
                        || c.Type = ClaimTypes.NameIdentifier)
                    |> Option.map (_.Value)
                    |> Result.requireSome SecureControllerError.EmailClaimMissing

                let username =
                    user.Claims
                    |> Seq.tryFind (fun c -> c.Type = "user_metadata")
                    |> Option.map (fun c ->
                        JsonSerializer
                            .Deserialize<{| username: string |}>(c.Value)
                            .username)
                    |> Option.defaultWith (fun _ ->
                        user.Claims
                        |> Seq.find (fun c -> c.Type = ClaimTypes.Name)
                        |> _.Value)

                let model = {
                    SecureModel.UserId = userId
                    Username = username
                }

                return model
            }

        match model with
        | Ok(m) ->
            let url = linkGenerator.GetUriByAction(ctxAccessor.HttpContext, "Index", "Secure")
            this.Response.Headers.Add("HX-Push-Url", url)
            this.View(m)
        | Error(SecureControllerError.UserNotFound) -> this.NotFound()
        | Error(SecureControllerError.EmailClaimMissing) -> this.BadRequest()
