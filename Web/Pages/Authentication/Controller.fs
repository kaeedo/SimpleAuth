namespace Web.Pages.Authentication

open Microsoft.AspNetCore.Mvc
open Web.Models
open Web.Services

type AuthenticationController(authService: AuthService) =
    inherit Controller()

    [<HttpGet>]
    member this.SignIn() = this.View()


    [<HttpGet>]
    member this.SignUp() = this.View()

    [<HttpPost>]
    [<ValidateAntiForgeryToken>]
    member this.SignUp([<FromForm>] signUp: SignUpPostModel) =
        task {
            let! wef = authService.SignUp(signUp.Email, signUp.Username, signUp.Password)
            return this.View()
        }
