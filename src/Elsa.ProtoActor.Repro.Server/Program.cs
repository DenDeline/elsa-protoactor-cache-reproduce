using Elsa;
using Elsa.Actors.ProtoActor.Features;
using Elsa.Actors.ProtoActor.HostedServices;
using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using MudBlazor.Services;
using Proto.Cluster.Dashboard;
using Proto.Remote;

// This local evidence host intentionally has no identity provider.
EndpointSecurityOptions.DisableSecurity();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var postgreSqlConnectionString = configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'elsa' is required.");

builder.Services.AddElsa(elsa =>
{
    elsa
        .UseWorkflowManagement(management =>
        {
            management.UseEntityFrameworkCore(ef =>
            {
                ef.UsePostgreSql(postgreSqlConnectionString);
            });
            management.UseCache();
        })
        .UseWorkflowRuntime(runtime =>
        {
            runtime.UseEntityFrameworkCore(ef =>
            {
                ef.UsePostgreSql(postgreSqlConnectionString);
            });
            runtime.UseProtoActor();
            runtime.UseCache();
        })
        .UseWorkflowsApi()
        .UseDistributedCache(distributedCache => distributedCache.UseProtoActor())
        .Configure<FixedProtoActorFeature>(protoActor =>
        {
            protoActor.ClusterName = "elsa-protoactor-cache-repro";
            protoActor.ConfigureRemoteConfig = _ => RemoteConfig
                .BindToLocalhost()
                .WithRemoteDiagnostics(true);
        });
});

builder.Services.AddProtoActorDashboard(new DashboardSettings
{
    LogSearchPattern = string.Empty,
    TraceSearchPattern = string.Empty
});
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseWorkflowsApi();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

// ISSUE: https://github.com/elsa-workflows/elsa-extensions/issues/167
class FixedProtoActorFeature(IModule module) : ProtoActorFeature(module)
{
    public override void ConfigureHostedServices()
    {
        Module.ConfigureHostedService<StartClusterMember>(-100);
    }
};