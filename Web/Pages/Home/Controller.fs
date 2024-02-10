namespace Web.Pages.Home

open Microsoft.AspNetCore.Mvc

type HomeController(supabaseClient: Supabase.Client) =
    inherit Controller()

    [<HttpGet>]
    member this.Index() =
        task {
            let wef = supabaseClient.Auth.CurrentSession

            return this.View(wef)
        }
