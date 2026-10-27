using Microsoft.Extensions.Options;

using MX.TripSideKick.Web.Options;

namespace MX.TripSideKick.Web.Hosting;

public sealed class RuntimeStateInitializer(IOptions<RuntimeStateOptions> state) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(state.Value.DocumentsDirectory))
        {
            Directory.CreateDirectory(state.Value.DocumentsDirectory);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
