using MedFlow.Api.Configuration;
using MedFlow.Core.Entities;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MedFlow.Api.Tests;

/// <summary>Production fail-fast for the JWT signing key, and no built-in seed credentials.</summary>
public class SecretsConfigurationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public SecretsConfigurationTests(TestApiFactory f) => _f = f;

    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "MedFlow.Api";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static readonly string StrongKey = new('k', 40);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_rejects_missing_key(string? key) =>
        Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.Validate(key, new FakeEnv("Production")));

    [Fact]
    public void Production_rejects_key_shorter_than_32() =>
        Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.Validate(new string('k', 31), new FakeEnv("Production")));

    [Theory]
    [InlineData("CHANGE_ME_to_a_long_random_value_0123456789")]
    [InlineData("your-super-secret-key-goes-here-0123456789")]
    [InlineData("This-Is-A-Placeholder-Value-0123456789ab")]
    public void Production_rejects_placeholder_key(string key) =>
        Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.Validate(key, new FakeEnv("Production")));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void NonDevelopment_accepts_strong_key_of_32_or_more(string env)
    {
        JwtKeyValidator.Validate(new string('k', 32), new FakeEnv(env));
        JwtKeyValidator.Validate(StrongKey, new FakeEnv(env));
    }

    [Fact]
    public void Error_message_does_not_reveal_key()
    {
        var key = "short-key-value";
        var ex = Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.Validate(key, new FakeEnv("Production")));
        Assert.DoesNotContain(key, ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Development_and_Testing_allow_short_key_but_not_empty(string env)
    {
        JwtKeyValidator.Validate("short", new FakeEnv(env));
        Assert.Throws<InvalidOperationException>(() => JwtKeyValidator.Validate("", new FakeEnv(env)));
    }

    [Fact]
    public async Task Seeder_creates_no_accounts_without_configured_credentials()
    {
        using var scope = _f.Services.CreateScope();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.DoesNotContain(users.Users.ToList(), u => u.Email == "patient.demo@medflow.local");
    }
}
