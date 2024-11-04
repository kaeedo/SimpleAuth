namespace Web.Services

open System
open System.Security.Claims
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Http

type AuthService(accessor: IHttpContextAccessor) =
    member _.SignIn(userId: Guid, username: string) =
        task {
            let identity =
                ClaimsIdentity(
                    seq {
                        Claim(ClaimTypes.NameIdentifier, userId.ToString())
                        Claim(ClaimTypes.Name, username)
                    },
                    CookieAuthenticationDefaults.AuthenticationScheme
                )

            let user = ClaimsPrincipal(identity)

            do! accessor.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, user)

            return ()
        }


    member _.SignOut() =
        task {
            do! accessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)

            return ()
        }
