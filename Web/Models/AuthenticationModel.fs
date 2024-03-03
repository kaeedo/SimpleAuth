namespace Web.Models

[<CLIMutable>]
type SignUpPostModel = {
    Email: string
    Username: string
    Password: string
}

[<CLIMutable>]
type SignInPostModel = { Email: string; Password: string }

type PasswordlessSignUpModel = { PublicKey: string }
