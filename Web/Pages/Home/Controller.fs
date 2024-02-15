namespace Web.Pages.Home

open Microsoft.AspNetCore.Mvc
open Web.Models

type HomeController(supabaseClient: Supabase.Client) =
    inherit Controller()

    [<HttpGet>]
    member this.Index() =
        let model = { HomeModel.IsLoggedIn = false }
        this.View(model)
