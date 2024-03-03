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

type AuthenticationController(config: IConfiguration, authService: SupabaseAuthService) =
    inherit Controller()

    [<HttpGet>]
    member this.SignIn() = this.View()

    [<HttpGet>]
    member this.PasswordlessSignUp() =
        task {

            return this.View()
        }

    [<HttpPost>]
    member this.PasswordlessSignUpWebAuthn([<FromForm>] username: string) : Task<JsonResult> =
        task {
            let payload = {|
                userId = Guid.NewGuid
                username = username
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

            return JsonResult(token)
        }

    [<HttpPost>]
    [<ValidateAntiForgeryToken>]
    member this.SignIn([<FromForm>] signIn: SignUpPostModel) =
        task {
            do! authService.SignIn(signIn.Email, signIn.Password)

            return this.RedirectToAction("Index", "Home")
        }

    [<HttpGet>]
    member this.SignUp() = this.View()

    [<HttpPost>]
    [<ValidateAntiForgeryToken>]
    member this.SignUp([<FromForm>] signUp: SignUpPostModel) =
        task {
            do! authService.SignUp(signUp.Email, signUp.Username, signUp.Password)
            return this.RedirectToAction(nameof this.ConfirmEmail)
        }

    [<HttpGet>]
    member this.ConfirmEmail() = this.View()

    [<HttpGet>]
    member this.Confirmed() =
        let queryParams = base.Request.Query

        task {
            if queryParams |> Seq.isEmpty |> not then
                let accessToken = queryParams["access_token"]
                let refreshToken = queryParams["refresh_token"]

                do! authService.SignInWithTokens(accessToken, refreshToken)

            return this.View()
        }

    [<HttpGet>]
    member this.Logout() =
        task {
            do! authService.SignOut()

            return this.RedirectToAction("Index", "Home")
        }
