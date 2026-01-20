open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe
open Giraffe.EndpointRouting

let endpoints =
    [ GET [ route "/hello" (text "Hello from Giraffe with Endpoint Routing!") ] ]

let configureApp (appBuilder: IApplicationBuilder) =
    appBuilder.UseRouting().UseGiraffe(endpoints) |> ignore

let configureServices (services: IServiceCollection) =
    services.AddRouting().AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    configureApp app
    app.Run()

    0
