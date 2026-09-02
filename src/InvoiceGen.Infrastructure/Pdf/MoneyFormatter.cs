using System.Globalization;

namespace InvoiceGen.Infrastructure.Pdf;

// Formats money using the document's own currency and a fixed (invariant) locale,
// so output never depends on the host machine's culture (x-rendering-policy):
// - decimal places follow the currency (USD 2, JPY 0, BHD 3, …), not a fixed 2
// - grouping/decimal separators are invariant (always "1,234.56"), not host-locale
internal static class MoneyFormatter
{
    // ISO 4217 minor-unit exceptions; everything not listed defaults to 2.
    private static readonly Dictionary<string, int> DecimalsByCurrency = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JPY"] = 0, ["KRW"] = 0, ["VND"] = 0, ["CLP"] = 0, ["ISK"] = 0,
        ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0, ["UGX"] = 0, ["RWF"] = 0,
        ["BHD"] = 3, ["KWD"] = 3, ["OMR"] = 3, ["TND"] = 3, ["JOD"] = 3,
        ["IQD"] = 3, ["LYD"] = 3,
    };

    public static string Format(string? currency, decimal amount)
    {
        var code = (currency ?? string.Empty).Trim().ToUpperInvariant();
        var decimals = DecimalsByCurrency.GetValueOrDefault(code, 2);
        var number = amount.ToString("N" + decimals, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(code) ? number : $"{code} {number}";
    }

    // Quantities aren't money, but must also be host-locale-independent.
    public static string Quantity(decimal quantity) =>
        quantity.ToString("0.##", CultureInfo.InvariantCulture);
}
