namespace Web.Controllers

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading.Tasks
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Web.Models
open Web.Services

type PasswordlessAuthenticationController
    (config: IConfiguration, passwordlessService: PasswordlessService, fakeDb: FakeDatabase) =
    inherit Controller()

    [<HttpGet>]
    member this.SignIn() =
        let model = {
            PasswordlessModel.PublicKey = config["Passwordless:PublicKey"].ToString()
        }

        this.View(model)

    [<HttpGet>]
    member this.SignUp() =
        let model = {
            PasswordlessModel.PublicKey = config["Passwordless:PublicKey"].ToString()
        }

        this.View(model)

    [<HttpGet>]
    member this.WebAuthnSignIn([<FromQuery>] token: string) =
        task {
            let content = JsonSerializer.Serialize({| token = token |})
            let content = new StringContent(content, Encoding.UTF8, "application/json")

            let url =
                config["Passwordless:BaseUrl"].ToString()
                + "/signin/verify"

            let secretKey = config["Passwordless:PrivateKey"].ToString()

            use client = new HttpClient()
            client.DefaultRequestHeaders.Add("ApiSecret", secretKey)

            let! response = client.PostAsync(url, content)
            let! response = response.Content.ReadAsStringAsync()

            let body = JsonSerializer.Deserialize<{| success: bool; userId: Guid |}>(response)

            if body.success then
                do! passwordlessService.SignIn(body.userId)

                return this.RedirectToAction("Index", "Home")
            else
                return this.RedirectToAction("SignIn")
        }

    [<HttpPost>]
    member this.WebAuthnSignUp([<FromBody>] body: {| username: string |}) : Task<JsonResult> =
        task {
            let payload = {|
                userId = Guid.NewGuid()
                username = body.username
            |}

            use client = new HttpClient()

            let content = JsonSerializer.Serialize(payload)
            let content = new StringContent(content, Encoding.UTF8, "application/json")

            let url =
                config["Passwordless:BaseUrl"].ToString()
                + "/register/token"

            let secretKey = config["Passwordless:PrivateKey"].ToString()

            client.DefaultRequestHeaders.Add("ApiSecret", secretKey)

            let! response = client.PostAsync(url, content)
            let! response = response.Content.ReadAsStringAsync()

            let token =
                JsonSerializer
                    .Deserialize<{| token: string |}>(response)
                    .token

            return
                JsonResult(
                    {|
                        registrationToken = token
                        userId = payload.userId
                    |}
                )
        }

    [<HttpPost>]
    member this.CreateUser([<FromBody>] body: {| userId: Guid; username: string |}) : JsonResult =
        JsonResult(
            {|
                success = fakeDb.CreateUser(body.userId, body.username)
            |}
        )

    [<HttpGet>]
    member this.Logout() =
        task {
            do! passwordlessService.SignOut()

            return this.RedirectToAction("Index", "Home")
        }
