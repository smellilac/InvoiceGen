namespace InvoiceGen.Application.Features.Customers;

// Single source of truth for customer field max lengths — used by both the validators
// (422 on oversized input) and the EF configuration (DB column length) so they can't drift.
public static class CustomerLimits
{
    public const int NameMax = 120;
    public const int EmailMax = 80;
    public const int AddressMax = 200;
    public const int PhoneMax = 30;
    public const int NotesMax = 2000;
}
