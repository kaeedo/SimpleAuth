namespace Web.Pages.Home

open Microsoft.AspNetCore.Mvc

type HomeController() =
    inherit Controller()

    [<HttpGet>]
    member this.Index() = this.View()
