using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceGen.Tests.Features.Documents;

public class GuestDocumentsTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private static object GuestDoc(
        object items, string? from = "Acme Inc\n1 Market St", string? to = "Client Co",
        string type = "invoice", decimal taxPercent = 0) => new
        {
            type,
            from,
            to,
            date = "2026-01-15",
            currency = "USD",
            items,
            tax_percent = taxPercent
        };

    private static object Item(string name, decimal qty, decimal unitCost) =>
        new { name, quantity = qty, unit_cost = unitCost };

    private async Task<int> DocumentRowCountAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // IgnoreQueryFilters so even a (hypothetical) soft-deleted row would be counted — we want
        // to prove the guest path writes NOTHING at all.
        return await db.Documents.IgnoreQueryFilters().CountAsync();
    }

    [Fact]
    public async Task Guest_ValidRequest_ReturnsPdf()
    {
        var client = factory.CreateClient(); // no bearer token

        var response = await client.PostAsJsonAsync("/documents/guest",
            GuestDoc(new[] { Item("Design", 2, 50m) }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4)); // PDF magic bytes
    }

    [Fact]
    public async Task Guest_MissingItems_Returns422WithValidationErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/documents/guest",
            GuestDoc(Array.Empty<object>()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        // Same ValidationErrorResponse shape as the authenticated create (both use ValidationFilter):
        // a problem-details body with a populated `errors` map.
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task Guest_MissingFrom_Returns422()
    {
        var client = factory.CreateClient();

        // `from` is required for guests (no saved profile to fall back on).
        var response = await client.PostAsJsonAsync("/documents/guest",
            GuestDoc(new[] { Item("Design", 1, 10m) }, from: null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Guest_DoesNotPersistAnyDocument()
    {
        var client = factory.CreateClient();

        var before = await DocumentRowCountAsync();

        var response = await client.PostAsJsonAsync("/documents/guest",
            GuestDoc(new[] { Item("Design", 2, 50m) }));
        response.EnsureSuccessStatusCode();

        // Query the table directly — the response says PDF, but prove nothing hit the database.
        var after = await DocumentRowCountAsync();
        Assert.Equal(before, after);
        Assert.Equal(0, after);
    }

    [Fact]
    public async Task Guest_IsNotRejectedAsUnauthorized_WhileAuthenticatedCreateStillIs()
    {
        var client = factory.CreateClient(); // no bearer token on either call

        var guest = await client.PostAsJsonAsync("/documents/guest",
            GuestDoc(new[] { Item("Design", 1, 10m) }));
        // The guest route is excluded from the group's bearer-token requirement.
        Assert.NotEqual(HttpStatusCode.Unauthorized, guest.StatusCode);
        Assert.Equal(HttpStatusCode.OK, guest.StatusCode);

        // The authenticated create endpoint still rejects an anonymous caller.
        var authed = await client.PostAsJsonAsync("/documents",
            new { type = "invoice", to = "Client Co", date = "2026-01-15", currency = "USD", items = new[] { Item("Design", 1, 10m) } });
        Assert.Equal(HttpStatusCode.Unauthorized, authed.StatusCode);
    }
}

// Uses the rate-limited factory (per-IP limiter force-enabled) in its own class so the tighter
// guest window is exercised in isolation and doesn't throttle the rest of the suite.
public class GuestDocumentsRateLimitTests(RateLimitedTestWebAppFactory factory)
    : IClassFixture<RateLimitedTestWebAppFactory>
{
    [Fact]
    public async Task Guest_ExceedingRateLimit_Returns429()
    {
        var client = factory.CreateClient();
        object body = new
        {
            type = "invoice",
            from = "Acme Inc",
            to = "Client Co",
            date = "2026-01-15",
            currency = "USD",
            items = new[] { new { name = "Design", quantity = 1m, unit_cost = 10m } },
        };

        // 10 requests/minute/IP (x-guest-document-policy). All calls share one IP partition, so the
        // 11th within the window is throttled.
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/documents/guest", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
