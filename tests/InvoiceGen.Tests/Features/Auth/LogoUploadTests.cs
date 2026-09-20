using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Auth;

public class LogoUploadTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // A valid 1x1 PNG — the smallest real image the server can decode and re-encode.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { email, password = "P@ssw0rd123", business_name = "Acme" });
        response.EnsureSuccessStatusCode();
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return body.GetProperty("access_token").GetString()!;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName); // field name "file" binds to the IFormFile parameter
        return content;
    }

    private HttpRequestMessage AuthedUpload(string token, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/me/logo") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task Upload_ValidPng_SetsLogoUrl_AndImageIsRetrievable()
    {
        var token = await RegisterAndGetTokenAsync("logo-ok@test.com");

        using var request = AuthedUpload(token, FileContent(TinyPng, "image/png", "logo.png"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var logoUrl = body.GetProperty("logo_url").GetString();
        Assert.False(string.IsNullOrEmpty(logoUrl));
        Assert.Contains("/auth/logo/", logoUrl);

        // The URL must be loadable without an Authorization header (as a plain <img> would).
        var image = await _client.GetAsync(logoUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_NonImage_Returns422()
    {
        var token = await RegisterAndGetTokenAsync("logo-bad@test.com");

        // Declares image/png, but the bytes are plain text — the server sniffs the actual
        // bytes (it can't decode them) and rejects, ignoring the declared content type.
        var bytes = Encoding.UTF8.GetBytes("this is definitely not an image");
        using var request = AuthedUpload(token, FileContent(bytes, "image/png", "logo.png"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_OverSize_Returns422()
    {
        var token = await RegisterAndGetTokenAsync("logo-big@test.com");

        var bytes = new byte[5 * 1024 * 1024 + 1]; // one byte over the 5 MB limit
        using var request = AuthedUpload(token, FileContent(bytes, "image/png", "logo.png"));
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WithoutToken_ReturnsUnauthorized()
    {
        using var content = FileContent(TinyPng, "image/png", "logo.png");
        var response = await _client.PostAsync("/auth/me/logo", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLogo_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/auth/logo/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ClearsLogo()
    {
        var token = await RegisterAndGetTokenAsync("logo-del@test.com");

        using var upload = AuthedUpload(token, FileContent(TinyPng, "image/png", "logo.png"));
        (await _client.SendAsync(upload)).EnsureSuccessStatusCode();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/auth/me/logo");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(delete);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(JsonValueKind.Null, body.GetProperty("logo_url").ValueKind);
    }
}
