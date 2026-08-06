using System.Globalization;

namespace MX.TripSideKick.Domain.Common;

/// <summary>
/// Validates ISO 4217 currency codes against the codes known to .NET's culture data, without a
/// hand-maintained list or an extra package dependency.
/// </summary>
public static class IsoCurrencyCodes
{
    private static readonly Lazy<HashSet<string>> KnownCodes = new(BuildKnownCodes);

    public static bool IsValid(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
        {
            return false;
        }

        return KnownCodes.Value.Contains(currencyCode.ToUpperInvariant());
    }

    private static HashSet<string> BuildKnownCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                if (!string.IsNullOrWhiteSpace(region.ISOCurrencySymbol) && region.ISOCurrencySymbol.Length == 3)
                {
                    codes.Add(region.ISOCurrencySymbol);
                }
            }
            catch (ArgumentException)
            {
                // Some specific cultures (e.g. neutral-only or invariant-adjacent) throw when
                // constructing a RegionInfo; skip them rather than fail currency validation.
            }
        }

        return codes;
    }
}
