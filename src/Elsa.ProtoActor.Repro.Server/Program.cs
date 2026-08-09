using Elsa;
using Elsa.Actors.ProtoActor.Features;
using Elsa.Actors.ProtoActor.HostedServices;
using Elsa.Extensions;
using Elsa.Features.Services;
using Elsa.Persistence.EFCore.Extensions;
using Elsa.Persistence.EFCore.Modules.Management;
using Elsa.Persistence.EFCore.Modules.Runtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Services;
using Proto.Cluster;
using Proto.Cluster.Dashboard;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.HealthChecks;

// This local evidence host intentionally has no identity provider.
EndpointSecurityOptions.DisableSecurity();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

var postgreSqlConnectionString = configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is required.");

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
            protoActor.ClusterName = configuration["ProtoActor:ClusterName"]
                ?? "elsa-protoactor-cache-repro";

            var remoteConfig = RemoteConfig.BindToLocalhost();

            if (!string.IsNullOrWhiteSpace(configuration["KUBERNETES_SERVICE_HOST"]))
            {
                var advertisedHost = configuration["ProtoActor:AdvertisedHost"];
                if (string.IsNullOrWhiteSpace(advertisedHost))
                {
                    throw new InvalidOperationException(
                        "ProtoActor:AdvertisedHost is required when running in Kubernetes.");
                }

                var clusterProvider = new KubernetesProvider(new KubernetesProviderConfig());
                protoActor.CreateClusterProvider = _ => clusterProvider;
                remoteConfig = RemoteConfig.BindToAllInterfaces(advertisedHost: advertisedHost);
            }

            remoteConfig = remoteConfig
                .WithLogLevelForDeserializationErrors(LogLevel.Critical)
                .WithRemoteDiagnostics(true);
            protoActor.ConfigureRemoteConfig = _ => remoteConfig;
        });
});

builder.Services
    .AddHealthChecks()
    .AddCheck<ActorSystemHealthCheck>(
        "proto-actor-system",
        tags: ["liveness"])
    .AddCheck<ClusterHealthCheck>(
        "proto-actor-cluster",
        tags: ["readiness", "startup"]);

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
app.MapHealthChecks(
    "/utils/hcheck/liveness",
    CreateProbeOptions("liveness"));
app.MapHealthChecks(
    "/utils/hcheck/readiness",
    CreateProbeOptions("readiness", rejectDegraded: true));
app.MapHealthChecks(
    "/utils/hcheck/startup",
    CreateProbeOptions("startup", rejectDegraded: true));
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();

static HealthCheckOptions CreateProbeOptions(string tag, bool rejectDegraded = false)
{
    var options = new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains(tag)
    };

    if (rejectDegraded)
    {
        options.ResultStatusCodes[HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable;
    }

    return options;
}

// ISSUE: https://github.com/elsa-workflows/elsa-extensions/issues/167
class FixedProtoActorFeature(IModule module) : ProtoActorFeature(module)
{
    public override void ConfigureHostedServices()
    {
        Module.ConfigureHostedService<StartClusterMember>(-100);
    }
};
