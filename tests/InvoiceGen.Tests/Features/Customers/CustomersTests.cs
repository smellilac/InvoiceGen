using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Customers;

public class CustomersTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
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

    private static object NewCustomer(string name, string? address = "1 Main St") =>
        new { name, email = "c@test.com", address, phone = "555-0100", notes = "vip" };

    private static async Task<string> CreateCustomerAsync(HttpClient client, string name, string? address = "1 Main St")
    {
        var res = await client.PostAsJsonAsync("/customers", NewCustomer(name, address));
        res.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetString()!;
    }

    private static object DocWithCustomer(string customerId) => new
    {
        type = "invoice",
        customer_id = customerId,
        date = "2026-01-15",
        currency = "USD",
        items = new[] { new { name = "Widget", quantity = 1, unit_cost = 10m } },
        tax_percent = 0
    };

    // ---- CRUD ----

    [Fact]
    public async Task Create_ReturnsCreatedWithFields()
    {
        var client = await AuthedClientAsync("cust-create@test.com");
        var response = await client.PostAsJsonAsync("/customers", NewCustomer("Alice"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Alice", body.GetProperty("name").GetString());
        Assert.Equal("vip", body.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/customers", NewCustomer("Alice"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsValidationError()
    {
        var client = await AuthedClientAsync("cust-noname@test.com");
        var response = await client.PostAsJsonAsync("/customers", new { name = "", email = "x@test.com" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOwnCustomers_OrderedByName()
    {
        var client = await AuthedClientAsync("cust-list@test.com");
        await client.PostAsJsonAsync("/customers", NewCustomer("Zoe"));
        await client.PostAsJsonAsync("/customers", NewCustomer("Anna"));

        var response = await client.GetAsync("/customers");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("Anna", body[0].GetProperty("name").GetString()); // name ASC
        Assert.Equal("Zoe", body[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Get_OtherUsersCustomer_Returns404()
    {
        var owner = await AuthedClientAsync("cust-owner@test.com");
        var id = await CreateCustomerAsync(owner, "Alice");

        var stranger = await AuthedClientAsync("cust-stranger@test.com");
        var response = await stranger.GetAsync($"/customers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesSuppliedFieldsOnly()
    {
        var client = await AuthedClientAsync("cust-update@test.com");
        var id = await CreateCustomerAsync(client, "Alice");

        var patch = await client.PatchAsJsonAsync($"/customers/{id}", new { phone = "555-9999" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var body = JsonDocument.Parse(await patch.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Alice", body.GetProperty("name").GetString());       // unchanged
        Assert.Equal("555-9999", body.GetProperty("phone").GetString());   // changed
    }

    [Fact]
    public async Task Delete_SoftDeletes_HidesFromListAndGet()
    {
        var client = await AuthedClientAsync("cust-delete@test.com");
        var id = await CreateCustomerAsync(client, "Alice");

        var del = await client.DeleteAsync($"/customers/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync($"/customers/{id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        var list = await client.GetAsync("/customers");
        var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(0, body.GetArrayLength());
    }

    // ---- Cross-cutting with Documents (x-customer-policy) ----

    [Fact]
    public async Task CreateDocument_WithCustomerId_SnapshotsToFromCustomer()
    {
        var client = await AuthedClientAsync("cust-snap@test.com");
        var id = await CreateCustomerAsync(client, "Alice Corp", "42 Market St");

        var doc = await client.PostAsJsonAsync("/documents", DocWithCustomer(id)); // no `to`
        Assert.Equal(HttpStatusCode.Created, doc.StatusCode);
        var body = JsonDocument.Parse(await doc.Content.ReadAsStringAsync()).RootElement;
        var to = body.GetProperty("to").GetString();
        Assert.Contains("Alice Corp", to);
        Assert.Contains("42 Market St", to);
    }

    [Fact]
    public async Task CreateDocument_ReferencingDeletedCustomer_Returns422()
    {
        var client = await AuthedClientAsync("cust-deldoc@test.com");
        var id = await CreateCustomerAsync(client, "Alice");
        await client.DeleteAsync($"/customers/{id}");

        var doc = await client.PostAsJsonAsync("/documents", DocWithCustomer(id));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, doc.StatusCode);
    }

    [Fact]
    public async Task DocumentTo_IsFrozen_AfterCustomerEdited()
    {
        var client = await AuthedClientAsync("cust-frozen@test.com");
        var id = await CreateCustomerAsync(client, "Original Name", "Old Address");

        var created = await client.PostAsJsonAsync("/documents", DocWithCustomer(id));
        var docId = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("id").GetString();

        // Edit the customer AFTER the document was created.
        await client.PatchAsJsonAsync($"/customers/{id}", new { name = "Renamed", address = "New Address" });

        var fetched = await client.GetAsync($"/documents/{docId}");
        var to = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync())
            .RootElement.GetProperty("to").GetString();

        Assert.Contains("Original Name", to); // snapshot frozen
        Assert.DoesNotContain("Renamed", to);
    }

    [Fact]
    public async Task ListDocuments_FilterByCustomerId()
    {
        var client = await AuthedClientAsync("cust-filter@test.com");
        var id = await CreateCustomerAsync(client, "Alice");

        await client.PostAsJsonAsync("/documents", DocWithCustomer(id));
        await client.PostAsJsonAsync("/documents", DocWithCustomer(id));
        // one document without a customer
        await client.PostAsJsonAsync("/documents", new
        {
            type = "invoice",
            to = "Someone Else",
            date = "2026-01-15",
            currency = "USD",
            items = new[] { new { name = "X", quantity = 1, unit_cost = 5m } },
            tax_percent = 0
        });

        var response = await client.GetAsync($"/documents?customer_id={id}");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(2, body.GetProperty("total").GetInt32());
    }
}
