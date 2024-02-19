namespace Web.Controllers

open System
open Microsoft.AspNetCore.Mvc
open Web.Models
open Web.Services

type AuthenticationController(authService: SupabaseAuthService) =
    inherit Controller()

    [<HttpGet>]
    member this.SignIn() = this.View()

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
