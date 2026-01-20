open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Giraffe

let notFoundHandler = "Not Found" |> text |> RequestErrors.notFound

let endpoints =
    choose
        [ GET >=> route "/hello" >=> text "Hello from Giraffe with Endpoint Routing!"
          notFoundHandler ]

let configureApp (appBuilder: IApplicationBuilder) = appBuilder.UseGiraffe(endpoints)

let configureServices (services: IServiceCollection) = services.AddGiraffe() |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services

    let app = builder.Build()

    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()

    0
