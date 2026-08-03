using System.Globalization;

namespace MX.TripSideKick.Domain.Common;

/// <summary>
/// A monetary amount expressed as an exact decimal in a specific ISO 4217 currency.
/// </summary>
/// <remarks>
/// Money is never represented as a floating point value, and amounts in different currencies
/// are never combined implicitly — conversion is an explicit, rate-aware operation.
/// </remarks>
public readonly record struct Money
{
    public Money(decimal amount, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        if (currencyCode.Length != 3)
        {
            throw new ArgumentException("Currency code must be a three letter ISO 4217 code.", nameof(currencyCode));
        }

        Amount = amount;
        CurrencyCode = currencyCode.ToUpperInvariant();
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public static Money Zero(string currencyCode) => new(0m, currencyCode);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, CurrencyCode);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, CurrencyCode);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount} {CurrencyCode}");

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(CurrencyCode, other.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in '{CurrencyCode}' and '{other.CurrencyCode}' without an explicit conversion.");
        }
    }
}
