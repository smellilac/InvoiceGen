using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Documents;

public class SettlementTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register",
            new { email, password = "P@ssw0rd123", business_name = "Acme" });
        reg.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await reg.Content.ReadAsStringAsync())
            .RootElement.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // A $100 document (2 x $50, no tax), amount_settled seeded via the create request.
    private static object Doc(string type = "invoice", decimal amountSettled = 0m) => new
    {
        type,
        to = "Client Co",
        date = "2026-01-15",
        currency = "USD",
        items = new[] { new { name = "Design", quantity = 2m, unit_cost = 50m } },
        tax_percent = 0,
        amount_settled = amountSettled
    };

    private async Task<string> CreateDocAsync(HttpClient client, string type = "invoice", decimal amountSettled = 0m)
    {
        var res = await client.PostAsJsonAsync("/documents", Doc(type, amountSettled));
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Settle_WithoutToken_ReturnsUnauthorized()
    {
        var owner = await AuthedClientAsync("settle-noauth@test.com");
        var id = await CreateDocAsync(owner);

        var anon = factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 10m });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Settle_OtherUsersDocument_Returns404()
    {
        var owner = await AuthedClientAsync("settle-owner@test.com");
        var id = await CreateDocAsync(owner);

        var stranger = await AuthedClientAsync("settle-stranger@test.com");
        var res = await stranger.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 10m });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Settle_PositiveAmount_AddsAndRecomputesBalance()
    {
        var client = await AuthedClientAsync("settle-positive@test.com");
        var id = await CreateDocAsync(client); // total 100, settled 0

        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 40m });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(40m, body.GetProperty("amount_settled").GetDecimal());
        Assert.Equal(60m, body.GetProperty("balance_remaining").GetDecimal());
    }

    [Fact]
    public async Task Settle_AmountsAccumulateAcrossCalls()
    {
        var client = await AuthedClientAsync("settle-accumulate@test.com");
        var id = await CreateDocAsync(client);

        await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 30m });
        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 25m });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(55m, body.GetProperty("amount_settled").GetDecimal());
        Assert.Equal(45m, body.GetProperty("balance_remaining").GetDecimal());
    }

    [Fact]
    public async Task Settle_NegativeAmount_CorrectsPreviousEntry()
    {
        var client = await AuthedClientAsync("settle-negative@test.com");
        var id = await CreateDocAsync(client, amountSettled: 50m);

        // A mistaken $50 becomes $30 by correcting -20.
        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = -20m });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(30m, body.GetProperty("amount_settled").GetDecimal());
        Assert.Equal(70m, body.GetProperty("balance_remaining").GetDecimal());
    }

    [Fact]
    public async Task Settle_ResultBelowZero_Returns422OnAmountField()
    {
        var client = await AuthedClientAsync("settle-belowzero@test.com");
        var id = await CreateDocAsync(client, amountSettled: 10m);

        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = -25m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Settle_ResultExceedsTotal_Returns422OnAmountField()
    {
        var client = await AuthedClientAsync("settle-exceeds@test.com");
        var id = await CreateDocAsync(client); // total 100

        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 150m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.True(body.GetProperty("errors").TryGetProperty("amount", out _));
    }

    [Fact]
    public async Task Settle_ExactlyToTotal_IsAllowed()
    {
        var client = await AuthedClientAsync("settle-exact@test.com");
        var id = await CreateDocAsync(client); // total 100

        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 100m });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(100m, body.GetProperty("amount_settled").GetDecimal());
        Assert.Equal(0m, body.GetProperty("balance_remaining").GetDecimal());
    }

    [Fact]
    public async Task Settle_CreditNote_RefundDirectionUsesSameArithmetic()
    {
        var client = await AuthedClientAsync("settle-creditnote@test.com");
        // For a credit_note, amount_settled means "refunded to the customer" — but the
        // add-and-clamp-to-[0, total] logic is identical to money-owed types.
        var id = await CreateDocAsync(client, type: "credit_note");

        var res = await client.PostAsJsonAsync($"/documents/{id}/settlement", new { amount = 100m });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(100m, body.GetProperty("amount_settled").GetDecimal());
        Assert.Equal(0m, body.GetProperty("balance_remaining").GetDecimal());
    }
}
