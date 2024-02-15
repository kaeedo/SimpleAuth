namespace Web.Services

open System
open System.Linq
open System.Collections.Generic
open System.Security.Claims
open System.Text.Json
open Microsoft.AspNetCore.Components.Authorization

type CustomAuthStateProvider(client: Supabase.Client) =
    inherit AuthenticationStateProvider()

    let parseBase64WithoutPadding (base64: string) =
        match base64.Length % 4 with
        | 2 -> Convert.FromBase64String(base64 + "==")
        | 3 -> Convert.FromBase64String(base64 + "=")
        | _ -> Convert.FromBase64String(base64)

    let parseClaimsFromJwt (jwt: string) =
        let payload = jwt.Split('.')[1]
        let jsonBytes = parseBase64WithoutPadding payload
        let kvp = JsonSerializer.Deserialize<Dictionary<string, obj>>(jsonBytes)

        kvp.Select(fun kv -> Claim(kv.Key, kv.Value.ToString()))

    override this.GetAuthenticationStateAsync() =
        task {
            let! _ = client.InitializeAsync()

            let identity =
                if
                    not <| isNull client.Auth.CurrentSession
                    && not
                       <| String.IsNullOrWhiteSpace(client.Auth.CurrentSession.AccessToken)
                then
                    ClaimsIdentity(parseClaimsFromJwt (client.Auth.CurrentSession.AccessToken), "jwt")
                else
                    ClaimsIdentity()

            let user = ClaimsPrincipal(identity)
            let state = AuthenticationState(user)

            return state
        }
