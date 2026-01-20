var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/hello", () => "Hello from ASP.NET Core Minimal API!");

app.Run();
