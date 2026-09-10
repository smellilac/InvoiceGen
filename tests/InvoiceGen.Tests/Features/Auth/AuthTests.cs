using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoiceGen.Tests.Features.Auth;

public class AuthTests(TestWebAppFactory factory) : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object NewRegistration(string email) =>
        new { email, password = "P@ssw0rd123", business_name = "Acme" };

    private async Task<JsonElement> RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/auth/register", NewRegistration(email));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Register_ReturnsCreatedWithTokensAndUser()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", NewRegistration("reg1@test.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refresh_token").GetString()));
        Assert.Equal("reg1@test.com", body.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        await RegisterAsync("dup@test.com");
        var second = await _client.PostAsJsonAsync("/auth/register", NewRegistration("dup@test.com"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidationError()
    {
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { email = "weak@test.com", password = "123", business_name = (string?)null });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        await RegisterAsync("login@test.com");
        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email = "login@test.com", password = "P@ssw0rd123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await RegisterAsync("wrongpw@test.com");
        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email = "wrongpw@test.com", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterTooManyFailures_LocksAccount()
    {
        await RegisterAsync("lockout@test.com");

        // 5 failed attempts hits the configured threshold
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/auth/login",
                new { email = "lockout@test.com", password = "wrong-password" });
        }

        // even the CORRECT password is now rejected because the account is locked
        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email = "lockout@test.com", password = "P@ssw0rd123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var reg = await RegisterAsync("me@test.com");
        var token = reg.GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("me@test.com", body.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokenPair()
    {
        var reg = await RegisterAsync("refresh@test.com");
        var refreshToken = reg.GetProperty("refresh_token").GetString();

        var response = await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Refresh_AfterRotation_OldTokenRejected()
    {
        var reg = await RegisterAsync("rotate@test.com");
        var refreshToken = reg.GetProperty("refresh_token").GetString();

        // first refresh succeeds and rotates (revokes) the old token
        await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = refreshToken });
        // reusing the same (now revoked) token must fail
        var reuse = await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.DeleteAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_RevokesEveryActiveSession_NotJustTheCaller()
    {
        var reg = await RegisterAsync("delete-sessions@test.com");
        var accessToken = reg.GetProperty("access_token").GetString();
        var firstSessionRefresh = reg.GetProperty("refresh_token").GetString();

        // a SECOND independent session for the same account
        var secondLogin = await _client.PostAsJsonAsync("/auth/login",
            new { email = "delete-sessions@test.com", password = "P@ssw0rd123" });
        secondLogin.EnsureSuccessStatusCode();
        var secondSessionRefresh = JsonDocument.Parse(await secondLogin.Content.ReadAsStringAsync())
            .RootElement.GetProperty("refresh_token").GetString();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/auth/me");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var deleteResponse = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // BOTH sessions are dead, not only the one that made the delete request
        var firstRefresh = await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = firstSessionRefresh });
        var secondRefresh = await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = secondSessionRefresh });
        Assert.Equal(HttpStatusCode.Unauthorized, firstRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefresh.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_ThenLogin_ReturnsUnauthorizedLikeBadCredentials()
    {
        var reg = await RegisterAsync("delete-login@test.com");
        var accessToken = reg.GetProperty("access_token").GetString();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/auth/me");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await _client.SendAsync(delete);

        // the (correct) password no longer works — a deleted account is indistinguishable
        // from a non-existent one
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email = "delete-login@test.com", password = "P@ssw0rd123" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_FreesEmailForReRegistration()
    {
        var reg = await RegisterAsync("delete-reuse@test.com");
        var accessToken = reg.GetProperty("access_token").GetString();

        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/auth/me");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await _client.SendAsync(delete);

        // the same address is available again — the person deliberately left
        var reRegister = await _client.PostAsJsonAsync("/auth/register", NewRegistration("delete-reuse@test.com"));
        Assert.Equal(HttpStatusCode.Created, reRegister.StatusCode);
    }

    [Fact]
    public async Task Logout_ThenRefresh_ReturnsUnauthorized()
    {
        var reg = await RegisterAsync("logout@test.com");
        var accessToken = reg.GetProperty("access_token").GetString();
        var refreshToken = reg.GetProperty("refresh_token").GetString();

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken })
        };
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await _client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refresh = await _client.PostAsJsonAsync("/auth/refresh", new { refresh_token = refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
