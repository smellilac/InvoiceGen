using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Documents;

public class EmailSendTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private async Task<HttpClient> AuthedClientAsync(string email)
    {
        var client = factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register",
            new { email, password = "P@ssw0rd123", business_name = "Acme Co" });
        reg.EnsureSuccessStatusCode();
        var token = JsonDocument.Parse(await reg.Content.ReadAsStringAsync())
            .RootElement.GetProperty("access_token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object InvoiceBody(string? customerId = null) => new
    {
        type = "invoice",
        customer_id = customerId,
        to = customerId is null ? "Client Co" : null,
        date = "2026-01-15",
        currency = "USD",
        items = new[] { new { name = "Widget", quantity = 1, unit_cost = 10m } },
        tax_percent = 0
    };

    private static async Task<string> CreateDocAsync(HttpClient client, string? customerId = null)
    {
        var res = await client.PostAsJsonAsync("/documents", InvoiceBody(customerId));
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateCustomerAsync(HttpClient client, string name, string? email)
    {
        var res = await client.PostAsJsonAsync("/customers", new { name, email });
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string?> PollSendStatusAsync(HttpClient client, string id, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var res = await client.GetAsync($"/documents/{id}");
            var status = JsonDocument.Parse(await res.Content.ReadAsStringAsync())
                .RootElement.GetProperty("last_send_status").GetString();
            if (status is not null && status != "queued") return status;
            await Task.Delay(100);
        }
        return null;
    }

    [Fact]
    public async Task Send_WithoutToken_ReturnsUnauthorized()
    {
        var owner = await AuthedClientAsync("send-noauth@test.com");
        var id = await CreateDocAsync(owner);

        var anon = factory.CreateClient();
        var res = await anon.PostAsJsonAsync($"/documents/{id}/send", new { to_email = "a@b.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Send_OtherUsersDocument_Returns404()
    {
        var owner = await AuthedClientAsync("send-owner@test.com");
        var id = await CreateDocAsync(owner);

        var stranger = await AuthedClientAsync("send-stranger@test.com");
        var res = await stranger.PostAsJsonAsync($"/documents/{id}/send", new { to_email = "a@b.com" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Send_NoResolvableRecipient_Returns422()
    {
        var client = await AuthedClientAsync("send-norecipient@test.com");
        var id = await CreateDocAsync(client); // explicit `to`, no customer

        var res = await client.PostAsJsonAsync($"/documents/{id}/send", new { }); // empty body
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Send_InvalidToEmail_Returns422()
    {
        var client = await AuthedClientAsync("send-bademail@test.com");
        var id = await CreateDocAsync(client);

        var res = await client.PostAsJsonAsync($"/documents/{id}/send", new { to_email = "not-an-email" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Send_WithToEmail_Returns202_AndMarksQueued()
    {
        var client = await AuthedClientAsync("send-toemail@test.com");
        var id = await CreateDocAsync(client);

        var res = await client.PostAsJsonAsync($"/documents/{id}/send", new { to_email = "customer@test.com" });

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, body.GetProperty("send_count").GetInt32());
        Assert.Equal("queued", body.GetProperty("last_send_status").GetString());
    }

    [Fact]
    public async Task Send_EmptyBody_ResolvesCustomerEmail_Returns202()
    {
        var client = await AuthedClientAsync("send-custemail@test.com");
        var customerId = await CreateCustomerAsync(client, "Alice", "alice@test.com");
        var id = await CreateDocAsync(client, customerId);

        var res = await client.PostAsJsonAsync($"/documents/{id}/send", new { }); // no to_email → use customer's
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
    }

    [Fact]
    public async Task Send_WorkerEventuallyMarksSent()
    {
        var client = await AuthedClientAsync("send-worker@test.com");
        var id = await CreateDocAsync(client);

        await client.PostAsJsonAsync($"/documents/{id}/send", new { to_email = "customer@test.com" });

        // The background worker (placeholder sender succeeds) should flip queued -> sent.
        var status = await PollSendStatusAsync(client, id, TimeSpan.FromSeconds(10));
        Assert.Equal("sent", status);
    }
}
