namespace SigmaChatServer

module LazyCreateUser =
    
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Http
    open System.Security.Claims
    open UserQueries

    type CreateUserMiddleware(next: RequestDelegate) =
        member _.InvokeAsync(ctx: HttpContext) : Task =
            task {
            let user = ctx.User

            if user.Identity <> null && user.Identity.IsAuthenticated then
                let sub =
                    user.FindFirst(ClaimTypes.NameIdentifier)
                    |> Option.ofObj
                    |> Option.map(fun c -> c.Value) 

                match sub with
                | Some auth0Sub -> let! _ = getOrCreateUser ctx auth0Sub
                                   ()
                | None -> ()

            return! next.Invoke(ctx)
            }
