namespace SigmaChatServer

module WebPush =
    open WebPush
    open Microsoft.AspNetCore.Http
    open Giraffe
    open ChatDb
    open WebPushDb
    open Microsoft.Extensions.Configuration
    open SigmaChatServer.Models
    open System.Text.Json.Nodes
    open Newtonsoft.Json.Linq
    open System.Threading.Tasks
    open System.Collections.Generic
    open System.Text.Json

    let private getVapidDetails (configuration: IConfiguration) =
        let vapidSection = configuration.GetSection("Vapid")
        let vapidPublic = vapidSection.["Public"]
        let vapidPrivate = vapidSection.["Private"]
        let vapidSubject = vapidSection.["Subject"]

        new VapidDetails(vapidSubject, vapidPublic, vapidPrivate)

    let private pushPayload (subscription: PushSubscription) (vapidDetails: VapidDetails) (payload: string) =
        task {
            let webPushClient = new WebPushClient()
            return! webPushClient.SendNotificationAsync(subscription, payload, vapidDetails)
        }

    let private parseSubscription (json: string) =
        let j = JObject.Parse json

        new PushSubscription(
            j.SelectToken "endpoint" |> string,
            j.SelectToken "keys.p256dh" |> string,
            j.SelectToken "keys.auth" |> string
        )

    let webpushMessageForUser (ctx: HttpContext) (userId: string) (createdMessageModel: CreateMessageModel) =
        task {
            let! subscriptionEntity = getSubscription ctx userId
            let configuration = ctx.GetService<IConfiguration>()

            let payload =
                JsonSerializer.Serialize
                    {| title = createdMessageModel.Text
                       // probable svg wont work todo test this
                       icon = "https://sigmachat.cc/cc.svg" |}

            return!
                match subscriptionEntity with
                // todo add logging
                | None -> task { return () }
                | Some sub ->
                    let parsedSubscription = parseSubscription sub.Json
                    let vapidDetails = getVapidDetails configuration
                    pushPayload parsedSubscription vapidDetails payload
        }

    let handleNewSubscription (next: HttpFunc) (ctx: HttpContext) =
        task {
            let userId = ctx.User.Identity.Name
            let! subJson = ctx.ReadBodyFromRequestAsync()

            do! insertSubscription ctx subJson userId

            return! json None next ctx
        }

    let handleGetVapidKey (next: HttpFunc) (ctx: HttpContext) =
        let configuration = ctx.GetService<IConfiguration>()

        let vapidSection = configuration.GetSection("Vapid")
        let vapidPublic = vapidSection.["Public"]

        task { return! json {| PublicKey = vapidPublic |} next ctx }

    let handlePushCustomMessage (next: HttpFunc) (ctx: HttpContext) =
        task {
            let! message = ctx.ReadBodyFromRequestAsync()
            let configuration = ctx.GetService<IConfiguration>()

            let! parsedSubscriptions = getAllSubscriptions ctx
            let vapidDetails = getVapidDetails configuration

            let subs =
                parsedSubscriptions |> List.ofSeq |> Seq.map (fun a -> parseSubscription a.Json)


            let payload =
                JsonSerializer.Serialize
                    {| title = message
                       options = {| body = message |} |}

            let! z =
                Task.WhenAll(
                    subs
                    |> Seq.map (fun subscription -> pushPayload subscription vapidDetails payload)
                )

            return! json None next ctx
        }