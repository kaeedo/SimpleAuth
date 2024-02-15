namespace Web.ViewComponents

open Microsoft.AspNetCore.Mvc
open Web.Models

type NavBarViewComponent() =
    inherit ViewComponent()

    member this.InvokeAsync() =
        let ctx = base.HttpContext

        task {
            let isLoggedIn = ctx.User.Identity.IsAuthenticated

            return this.View({ NavBarModel.IsLoggedIn = isLoggedIn })
        }
