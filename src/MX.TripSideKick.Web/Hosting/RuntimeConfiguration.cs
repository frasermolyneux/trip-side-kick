using System.Net;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;

namespace MX.TripSideKick.Web.Hosting;

public static class RuntimeConfiguration
{
    public static void Load(WebApplicationBuilder builder)
    {
        var path = Environment.GetEnvironmentVariable("TRIPSIDEKICK_RUNTIME_FILE")
            ?? "/run/secrets/runtime-configuration";
        if (File.Exists(path))
        {
            builder.Configuration.AddJsonFile(path, optional: false, reloadOnChange: false);
            builder.Configuration.AddEnvironmentVariables();
        }
        else if (Environment.GetEnvironmentVariable("TRIPSIDEKICK_RUNTIME_FILE") is not null)
        {
            throw new InvalidOperationException("The pinned runtime configuration file is missing.");
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var address in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                options.KnownProxies.Add(IPAddress.Parse(address));
            }
        });

        builder.Services.PostConfigureAll<MicrosoftIdentityOptions>(options =>
        {
            var certificateFile = builder.Configuration["AzureAd:ClientCertificateFile"];
            if (string.IsNullOrWhiteSpace(certificateFile))
            {
                return;
            }

            var certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(File.ReadAllText(certificateFile).Trim()), null,
                X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
                || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
            {
                throw new InvalidOperationException("The client certificate must contain a valid private key.");
            }

            options.ClientCertificates = [CertificateDescription.FromCertificate(certificate)];
        });
    }

    public static async Task<int> ProbeAsync(IConfiguration configuration, bool ready)
    {
        var host = configuration["Health:Host"];
        var configuredHosts = (configuration.GetSection("HostRouting:AppHosts").Get<string[]>() ?? [])
            .Concat(configuration.GetSection("HostRouting:SiteHosts").Get<string[]>() ?? []);
        if (string.IsNullOrWhiteSpace(host) || !configuredHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
        {
            return 1;
        }

        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"http://127.0.0.1:8080/api/health/{(ready ? "ready" : "live")}");
        request.Headers.Host = host;
        try
        {
            using var response = await client.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            return 1;
        }
        catch (TaskCanceledException)
        {
            return 1;
        }
    }
}
