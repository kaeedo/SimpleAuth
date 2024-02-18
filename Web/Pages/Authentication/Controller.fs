namespace Web.Pages.Authentication

open Microsoft.AspNetCore.Mvc
open Web.Models
open Web.Services

type AuthenticationController(authService: AuthService) =
    inherit Controller()

    [<HttpGet>]
    member this.SignIn() = this.View()

    [<HttpPost>]
    [<ValidateAntiForgeryToken>]
    member this.SignIn([<FromForm>] signIn: SignUpPostModel) =
        task {
            do! authService.SignIn(signIn.Email, signIn.Password)
            return this.View()
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
