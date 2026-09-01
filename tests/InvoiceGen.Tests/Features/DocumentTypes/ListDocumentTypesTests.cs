using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace InvoiceGen.Tests.Features.DocumentTypes;

public class ListDocumentTypesTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/document-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsAllElevenTypes()
    {
        var response = await _client.GetAsync("/document-types");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(11, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Get_EachTypeHasRequiredFields()
    {
        var response = await _client.GetAsync("/document-types");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        foreach (var type in doc.RootElement.EnumerateArray())
        {
            Assert.True(type.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 });
            Assert.True(type.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 });
            Assert.True(type.TryGetProperty("description", out var desc) && desc.GetString() is { Length: > 0 });
            Assert.True(type.TryGetProperty("icon", out var icon) && icon.GetString() is { Length: > 0 });
        }
    }

    [Fact]
    public async Task Get_ContainsInvoiceType()
    {
        var response = await _client.GetAsync("/document-types");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var ids = doc.RootElement.EnumerateArray()
            .Select(t => t.GetProperty("id").GetString())
            .ToList();

        Assert.Contains("invoice", ids);
    }

    [Fact]
    public async Task Get_IsPublic_NoAuthRequired()
    {
        var clientWithNoAuth = factory.CreateClient();
        clientWithNoAuth.DefaultRequestHeaders.Clear();

        var response = await clientWithNoAuth.GetAsync("/document-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
