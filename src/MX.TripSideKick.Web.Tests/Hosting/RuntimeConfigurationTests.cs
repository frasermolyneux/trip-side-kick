using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace MX.TripSideKick.Web.Tests.Hosting;

public sealed class RuntimeConfigurationTests
{
    [Fact]
    public void Mounted_base64_certificate_drives_the_confidential_client_without_browser_tokens()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Local disposable test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(AppContext.BaseDirectory, $"client-certificate-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, Convert.ToBase64String(certificate.Export(X509ContentType.Pfx)));
            using var factory = new TripSideKickApplicationFactory();
            using var configured = factory.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["AzureAd:ClientCertificateFile"] = path })));
            using var client = configured.CreateClient();
            var identity = configured.Services.GetRequiredService<IOptionsMonitor<MicrosoftIdentityOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme);
            using var loaded = Assert.Single(identity.ClientCertificates!).Certificate;
            Assert.NotNull(loaded);
            Assert.True(loaded.HasPrivateKey);
            Assert.Equal(certificate.Thumbprint, loaded.Thumbprint);
            var oidc = configured.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(OpenIdConnectDefaults.AuthenticationScheme);
            Assert.True(oidc.UsePkce);
            Assert.Equal("code", oidc.ResponseType);
            Assert.False(oidc.SaveTokens);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Production_without_database_configuration_fails_startup()
    {
        using var factory = new TripSideKickApplicationFactory();
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Sql:ConnectionString"] = string.Empty }));
        });
        Assert.Throws<OptionsValidationException>(() =>
        {
            using var client = production.CreateClient();
        });
    }

    [Theory]
    [InlineData("172.30.0.2", "https")]
    [InlineData("172.30.0.3", "http")]
    public async Task Only_the_configured_proxy_can_forward_scheme_and_never_host(string remoteAddress, string expectedScheme)
    {
        using var factory = new TripSideKickApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ForwardedHeaders:KnownProxies:0"] = "172.30.0.2" })));
        using var client = configured.CreateClient();
        var result = await configured.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            context.Request.Scheme = "http";
            context.Request.Host = new HostString(TripSideKickApplicationFactory.AppHost);
            context.Request.Path = "/api/health/live";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-Host"] = "untrusted.invalid";
        });
        Assert.Equal(expectedScheme, result.Request.Scheme);
        Assert.Equal(TripSideKickApplicationFactory.AppHost, result.Request.Host.Host);
    }
}
