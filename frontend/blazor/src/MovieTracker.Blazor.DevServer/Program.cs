// The development server of the Blazor frontend: serves the Blazor app on http://localhost:4200 and forwards
// /api to the backend, so that the backend needs no CORS setup and the session cookie works.
// This is what Angular's proxy.conf.json and Vite's server.proxy do for the other frontends.

var builder = WebApplication.CreateBuilder(args);

var backend = builder.Configuration["Backend:Url"] ?? "http://localhost:5080";

builder.Services
    .AddReverseProxy()
    .LoadFromMemory(
        [
            new Yarp.ReverseProxy.Configuration.RouteConfig
            {
                RouteId = "api",
                ClusterId = "backend",
                Match = new Yarp.ReverseProxy.Configuration.RouteMatch { Path = "/api/{**catch-all}" },
                // The backend builds the Google redirect URI from the Host header, which must stay the
                // one of this server (localhost:4200), as with the proxies of the other frontends.
                Transforms = [new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }],
            },
        ],
        [
            new Yarp.ReverseProxy.Configuration.ClusterConfig
            {
                ClusterId = "backend",
                Destinations = new Dictionary<string, Yarp.ReverseProxy.Configuration.DestinationConfig>
                {
                    ["backend"] = new() { Address = backend },
                },
            },
        ]);

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapReverseProxy();
// Blazor routes like /diary/123 are not files: answer them with index.html, the router takes over.
app.MapFallbackToFile("index.html");

app.Run();
