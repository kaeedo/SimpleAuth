namespace Web.Services

open System.Collections.Generic
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Http
open Supabase.Gotrue

type AuthService
    (client: Supabase.Client, customAuthStateProvider: AuthenticationStateProvider, accessor: IHttpContextAccessor) =
    member _.SignIn(username: string, password: string) =
        task {
            // check overloads for other sign in methods
            let! _ = client.Auth.SignIn(username, password)
            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authState.User)

            return ()
        }

    member _.SignUp(email: string, username: string, password: string) =
        task {
            // check overloads for other sign in methods

            let signUpOptions =
                ([ "username", (username :> obj) ] |> dict) :?> Dictionary<string, obj>

            let! _ = client.Auth.SignUp(email, password, SignUpOptions(Data = signUpOptions))
            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authState.User)

            return ()
        }

    member _.SignOut() =
        task {
            do! client.Auth.SignOut()

            let! _ = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)

            return ()
        }
