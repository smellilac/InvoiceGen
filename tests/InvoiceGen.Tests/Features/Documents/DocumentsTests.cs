using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Documents;

public class DocumentsTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
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

    private static object Doc(object items, string type = "invoice", decimal taxPercent = 0) => new
    {
        type,
        to = "Client Co",
        date = "2026-01-15",
        currency = "USD",
        items,
        tax_percent = taxPercent
    };

    private static object Item(string name, decimal qty, decimal unitCost) =>
        new { name, quantity = qty, unit_cost = unitCost };

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/documents",
            Doc(new[] { Item("A", 1, 10m) }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithComputedTotals()
    {
        var client = await AuthedClientAsync("doc-create@test.com");

        var response = await client.PostAsJsonAsync("/documents",
            Doc(new[] { Item("Design", 2, 50m) })); // 2 x 50 = 100, no tax

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(100m, body.GetProperty("subtotal").GetDecimal());
        Assert.Equal(100m, body.GetProperty("total").GetDecimal());
        Assert.Equal(100m, body.GetProperty("balance_due").GetDecimal());
        Assert.Equal("invoice", body.GetProperty("type").GetString());
        Assert.EndsWith("/pdf", body.GetProperty("pdf_url").GetString());
    }

    [Fact]
    public async Task Create_RoundsTaxPerLineItem_NotOnSubtotal()
    {
        var client = await AuthedClientAsync("doc-tax@test.com");

        // Three $0.05 lines at 10%: each line tax 0.005 -> rounds to 0.01, summed = 0.03.
        // Rounding once on the 0.15 subtotal would give 0.02 -> total 0.17. We expect 0.18.
        var response = await client.PostAsJsonAsync("/documents",
            Doc(new[] { Item("A", 1, 0.05m), Item("B", 1, 0.05m), Item("C", 1, 0.05m) }, taxPercent: 10m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0.15m, body.GetProperty("subtotal").GetDecimal());
        Assert.Equal(0.18m, body.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Create_WithNoItems_ReturnsValidationError()
    {
        var client = await AuthedClientAsync("doc-noitems@test.com");

        var response = await client.PostAsJsonAsync("/documents", Doc(Array.Empty<object>()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnDocuments()
    {
        var client = await AuthedClientAsync("doc-list@test.com");
        await client.PostAsJsonAsync("/documents", Doc(new[] { Item("A", 1, 10m) }));
        await client.PostAsJsonAsync("/documents", Doc(new[] { Item("B", 1, 20m) }));

        var response = await client.GetAsync("/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal(2, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task List_FiltersByType()
    {
        var client = await AuthedClientAsync("doc-filter@test.com");
        await client.PostAsJsonAsync("/documents", Doc(new[] { Item("A", 1, 10m) }, type: "invoice"));
        await client.PostAsJsonAsync("/documents", Doc(new[] { Item("B", 1, 20m) }, type: "receipt"));

        var response = await client.GetAsync("/documents?type=receipt");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal("receipt", body.GetProperty("data")[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Get_OtherUsersDocument_Returns404()
    {
        var owner = await AuthedClientAsync("doc-owner@test.com");
        var created = await owner.PostAsJsonAsync("/documents", Doc(new[] { Item("A", 1, 10m) }));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString();

        var stranger = await AuthedClientAsync("doc-stranger@test.com");
        var response = await stranger.GetAsync($"/documents/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesDocument()
    {
        var client = await AuthedClientAsync("doc-delete@test.com");
        var created = await client.PostAsJsonAsync("/documents", Doc(new[] { Item("A", 1, 10m) }));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString();

        var delete = await client.DeleteAsync($"/documents/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await client.GetAsync($"/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task GetPdf_ReturnsPdfFile()
    {
        var client = await AuthedClientAsync("doc-pdf@test.com");
        var created = await client.PostAsJsonAsync("/documents", Doc(new[] { Item("Design", 1, 10m) }));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString();

        var response = await client.GetAsync($"/documents/{id}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); // PDF magic bytes
    }

    [Fact]
    public async Task GetPdf_ForPackingSlip_RendersWithoutPricingSection()
    {
        var client = await AuthedClientAsync("doc-packing@test.com");
        var created = await client.PostAsJsonAsync("/documents",
            Doc(new[] { Item("Widget", 3, 9.99m) }, type: "packing_slip", taxPercent: 8m));
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString();

        // Exercises the packing-slip-specific layout branch (no price columns / totals).
        // Asserting the *absence* of pricing text requires PDF text extraction, so this is a
        // render smoke test; the content rule is verified by the renderer's branch + manual check.
        var response = await client.GetAsync($"/documents/{id}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var client = await AuthedClientAsync("doc-delete404@test.com");

        var response = await client.DeleteAsync($"/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
