using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MX.TripSideKick.Web.Pages;

/// <summary>Brochure landing page served on the <c>tripsidekick.net</c> surface.</summary>
public class IndexModel : PageModel
{
    /// <summary>Absolute URL of the application surface, kept environment-aware.</summary>
    public string AppUrl { get; private set; } = "https://tripsidekick.app/";

    public void OnGet()
    {
        var host = Request.Host.Host;

        if (host.StartsWith("dev.", StringComparison.OrdinalIgnoreCase))
        {
            AppUrl = "https://dev.tripsidekick.app/";
        }
        else if (host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            AppUrl = "/";
        }
    }
}
