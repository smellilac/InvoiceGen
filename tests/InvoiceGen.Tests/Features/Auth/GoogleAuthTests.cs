using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InvoiceGen.Tests.Features.Auth;

// "Continue with Google" (POST /auth/google). The real Google verification is swapped for
// FakeGoogleTokenVerifier so no test ever contacts Google; the fake encodes the verified
// payload (or a verification failure) in the posted "id token" string.
public class GoogleAuthTests : IClassFixture<TestWebAppFactory>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GoogleAuthTests(TestWebAppFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var toRemove = services.Where(d => d.ServiceType == typeof(IGoogleTokenVerifier)).ToList();
                foreach (var d in toRemove) services.Remove(d);
                services.AddScoped<IGoogleTokenVerifier, FakeGoogleTokenVerifier>();
            }));
        _client = _factory.CreateClient();
    }

    private Task<HttpResponseMessage> GoogleSignInAsync(string idToken) =>
        _client.PostAsJsonAsync("/auth/google", new { id_token = idToken });

    private static object PasswordRegistration(string email) =>
        new { email, password = "P@ssw0rd123", business_name = "Acme" };

    [Fact]
    public async Task NewEmail_CreatesAccountAndReturnsTokens()
    {
        var response = await GoogleSignInAsync(FakeGoogleTokenVerifier.Token("sub-new", "gnew@test.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refresh_token").GetString()));
        Assert.Equal("gnew@test.com", body.GetProperty("user").GetProperty("email").GetString());

        var user = await SingleUserByEmailAsync("gnew@test.com");
        Assert.Equal("sub-new", user.GoogleId);
        Assert.Null(user.PasswordHash); // Google-only account has no password
    }

    [Fact]
    public async Task ExistingPasswordAccount_IsLinkedNotDuplicated()
    {
        var register = await _client.PostAsJsonAsync("/auth/register", PasswordRegistration("glink@test.com"));
        register.EnsureSuccessStatusCode();

        var response = await GoogleSignInAsync(FakeGoogleTokenVerifier.Token("sub-link", "glink@test.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));

        // Linked onto the SAME row — GoogleId is now set and there's exactly one user for the email.
        var user = await SingleUserByEmailAsync("glink@test.com");
        Assert.Equal("sub-link", user.GoogleId);
        Assert.NotNull(user.PasswordHash); // the original password is untouched by linking
    }

    [Fact]
    public async Task RepeatGoogleSignIn_LogsIntoSameAccount()
    {
        var first = await GoogleSignInAsync(FakeGoogleTokenVerifier.Token("sub-repeat", "grepeat@test.com"));
        first.EnsureSuccessStatusCode();

        var second = await GoogleSignInAsync(FakeGoogleTokenVerifier.Token("sub-repeat", "grepeat@test.com"));

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // Still one row: the second sign-in matched on GoogleId, it didn't create another account.
        await SingleUserByEmailAsync("grepeat@test.com");
    }

    [Fact]
    public async Task UnverifiedEmail_IsRejected()
    {
        var response = await GoogleSignInAsync(
            FakeGoogleTokenVerifier.Token("sub-unverified", "gunv@test.com", emailVerified: false));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // Nothing was created off the unverified email.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Users.CountAsync(u => u.Email == "gunv@test.com"));
    }

    [Fact]
    public async Task InvalidToken_IsRejected()
    {
        var response = await GoogleSignInAsync(FakeGoogleTokenVerifier.InvalidToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PasswordLogin_StillWorks_ForAPasswordAccount()
    {
        var register = await _client.PostAsJsonAsync("/auth/register", PasswordRegistration("gpw@test.com"));
        register.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email = "gpw@test.com", password = "P@ssw0rd123" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task PasswordLogin_ForGoogleOnlyAccount_ReturnsDistinctError()
    {
        // A Google-only account (created via /auth/google, no password).
        var created = await GoogleSignInAsync(FakeGoogleTokenVerifier.Token("sub-only", "gonly@test.com"));
        created.EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email = "gonly@test.com", password = "anything-goes-here" });

        Assert.Equal(HttpStatusCode.Conflict, login.StatusCode);
        var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        // The problem-details title carries the machine-readable code the frontend switches on.
        Assert.Equal("account_uses_google_auth", body.GetProperty("title").GetString());
    }

    private async Task<InvoiceGen.Domain.Entities.AppUser> SingleUserByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.SingleAsync(u => u.Email == email);
    }
}
