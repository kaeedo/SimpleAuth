namespace Web.Pages.Home

open Microsoft.AspNetCore.Mvc
open Web.Models

type HomeController() =
    inherit Controller()

    [<HttpGet>]
    member this.Index() =
        let ctx = base.HttpContext

        let model = { HomeModel.IsLoggedIn = ctx.User.Identity.IsAuthenticated }
        this.View(model)
