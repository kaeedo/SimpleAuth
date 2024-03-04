namespace Web.Services

open System.Collections.Generic
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Http
open Supabase.Gotrue

type SupabaseAuthService
    (client: Supabase.Client, customAuthStateProvider: AuthenticationStateProvider, accessor: IHttpContextAccessor) =
    member _.SignIn(email: string, password: string) =
        task {
            // check overloads for other sign in methods
            let! _ = client.Auth.SignIn(email, password)
            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authState.User)

            return ()
        }

    member _.SignInWithTokens(accessToken: string, refreshToken: string) =
        task {
            let! _ = client.Auth.SetSession(accessToken, refreshToken)

            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()
            do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authState.User)

            return ()
        }

    member _.SignUp(email: string, username: string, password: string) =
        task {
            let signUpOptions: Dictionary<string, obj> =
                let d = Dictionary<string, obj>()

                d.Add("username", username :> obj)
                d

            let! session = client.Auth.SignUp(email, password, SignUpOptions(Data = signUpOptions))
            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()

            return ()
        }

    member _.SignOut() =
        task {
            do! client.Auth.SignOut()

            let! _ = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)

            return ()
        }
