namespace Web.Pages.Secure

open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc

[<Authorize>]
type SecureController() =
    inherit Controller()

    [<HttpGet>]
    member this.Index() = this.View()
