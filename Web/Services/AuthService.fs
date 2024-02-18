namespace Web.Services

open System.Collections.Generic
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Components.Authorization
open Microsoft.AspNetCore.Http
open Supabase.Gotrue
open Microsoft.Extensions.Configuration

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

            let signUpOptions: Dictionary<string, obj> =
                let d = Dictionary<string, obj>()
                d.Add("username", username :> obj)

                // https://supabase.com/docs/guides/auth/concepts/redirect-urls#vercel-preview-urls
                d.Add("redirectTo", "transform url here. must match supabse config" :> obj)
                d

            let! ewf = client.Auth.Settings()
            let few = client.Auth.Options

            let! session = client.Auth.SignUp(email, password, SignUpOptions(Data = signUpOptions))
            let! authState = customAuthStateProvider.GetAuthenticationStateAsync()

            //do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authState.User)

            return ()
        }

    member _.SignOut() =
        task {
            do! client.Auth.SignOut()

            let! _ = customAuthStateProvider.GetAuthenticationStateAsync()

            do! accessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)

            return ()
        }
