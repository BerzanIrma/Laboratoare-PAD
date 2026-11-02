using Gateway;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using Ocelot.Provider.Polly;

var builder = WebApplication.CreateBuilder(args);

// toate regulile de rutare stau in ocelot.json
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddOcelot(builder.Configuration)
    .AddConsul<IpConsulServiceBuilder>()   // inainte era .AddConsul()
    .AddPolly();
var app = builder.Build();
await app.UseOcelot();
app.Run();