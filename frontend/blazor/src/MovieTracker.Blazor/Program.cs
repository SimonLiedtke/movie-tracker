using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MovieTracker.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMovieTracker(new Uri(builder.HostEnvironment.BaseAddress));

var host = builder.Build();

// The pages need to know whether someone is signed in before the first one is shown.
await host.Services.GetRequiredService<AuthService>().LoadAsync();

await host.RunAsync();
