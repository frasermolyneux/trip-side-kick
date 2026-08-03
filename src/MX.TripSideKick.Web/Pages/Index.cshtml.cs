using Microsoft.AspNetCore.Mvc.RazorPages;

using MX.TripSideKick.Web.Hosting;

namespace MX.TripSideKick.Web.Pages;

/// <summary>Brochure landing page served on the <c>tripsidekick.net</c> surface.</summary>
public class IndexModel : PageModel
{
    /// <summary>Absolute URL of the application surface, kept environment-aware.</summary>
    public string AppUrl { get; private set; } = "https://tripsidekick.app/";

    public void OnGet()
    {
        AppUrl = AppSurfaceLinkResolver.Resolve(Request.Host.Host);
    }
}
