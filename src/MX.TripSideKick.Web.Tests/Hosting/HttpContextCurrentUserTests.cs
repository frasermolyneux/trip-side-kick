using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using MX.TripSideKick.Web.Hosting;

namespace MX.TripSideKick.Web.Tests.Hosting;

/// <summary>
/// Covers <see cref="HttpContextCurrentUser.VerifiedEmail"/>'s claim-precedence rules, in
/// particular the <c>emails</c> claim - some external identity providers behind Entra External ID
/// emit this as a JSON array (e.g. <c>["a@b.com"]</c>) rather than a plain string. A prior
/// implementation read it with <see cref="ClaimsPrincipal.FindFirstValue(string)"/> directly, which
/// returned the raw JSON text and could never match an invited email - breaking invitation-accept
/// email-binding for those IdPs. See docs/identity-and-access.md.
/// </summary>
public sealed class HttpContextCurrentUserTests
{
    [Fact]
    public void VerifiedEmail_prefers_the_plain_email_claim_when_present()
    {
        var user = CreateCurrentUser(
            ("email", "owner@example.com"),
            ("emails", "[\"other@example.com\"]"));

        Assert.Equal("owner@example.com", user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_parses_the_emails_claim_as_a_JSON_array()
    {
        var user = CreateCurrentUser(("emails", "[\"owner@example.com\",\"secondary@example.com\"]"));

        Assert.Equal("owner@example.com", user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_falls_back_to_the_raw_emails_value_when_it_is_not_valid_JSON()
    {
        var user = CreateCurrentUser(("emails", "owner@example.com"));

        Assert.Equal("owner@example.com", user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_skips_empty_entries_in_the_emails_array()
    {
        var user = CreateCurrentUser(("emails", "[\"\",\"owner@example.com\"]"));

        Assert.Equal("owner@example.com", user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_falls_back_to_preferred_username_when_it_looks_like_an_email()
    {
        var user = CreateCurrentUser(("preferred_username", "owner@example.com"));

        Assert.Equal("owner@example.com", user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_ignores_a_non_email_preferred_username()
    {
        var user = CreateCurrentUser(("preferred_username", "owner"));

        Assert.Null(user.VerifiedEmail);
    }

    [Fact]
    public void VerifiedEmail_is_null_when_not_authenticated()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()),
            },
        };

        var user = new HttpContextCurrentUser(httpContextAccessor);

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.VerifiedEmail);
    }

    private static HttpContextCurrentUser CreateCurrentUser(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "Test");

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            },
        };

        return new HttpContextCurrentUser(httpContextAccessor);
    }
}
