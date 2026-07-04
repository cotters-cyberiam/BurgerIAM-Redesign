using BurgerIAM.TestUtilities;
using Grpc.Core;
using IdentityService.Data;
using IdentityService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Tests;

public class IdentityGrpcServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IdentityGrpcService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "BurgerIAM-SuperSecret-Key-Min32Chars!"
            })
            .Build();
        return new IdentityGrpcService(db, config);
    }

    private static void SeedTokenVersion(AppDbContext db)
    {
        if (!db.TokenVersions.Any())
        {
            db.TokenVersions.Add(new TokenVersion
            {
                Version = Guid.NewGuid().ToString("N"),
                GeneratedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }

    [Fact]
    public async Task Register_CreatesUser()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var request = new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        };

        var response = await service.Register(request, new MockServerCallContext());

        Assert.False(string.IsNullOrWhiteSpace(response.UserId));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsError()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var request = new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        };

        await service.Register(request, new MockServerCallContext());
        var response = await service.Register(request, new MockServerCallContext());

        Assert.Equal("Email already registered", response.Error);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var db = CreateDbContext();
        SeedTokenVersion(db);
        var service = CreateService(db);

        await service.Register(new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        }, new MockServerCallContext());

        var response = await service.Login(new BurgerIAM.Protos.Identity.LoginRequest
        {
            Email = "test@test.com",
            Password = "password123"
        }, new MockServerCallContext());

        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("test@test.com", response.Email);
        Assert.Equal("Customer", response.Role);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsError()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        await service.Register(new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        }, new MockServerCallContext());

        var response = await service.Login(new BurgerIAM.Protos.Identity.LoginRequest
        {
            Email = "test@test.com",
            Password = "wrongpassword"
        }, new MockServerCallContext());

        Assert.Equal("Invalid email or password", response.Error);
    }

    [Fact]
    public async Task ValidateToken_ValidToken_ReturnsValid()
    {
        var db = CreateDbContext();
        SeedTokenVersion(db);
        var service = CreateService(db);

        await service.Register(new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        }, new MockServerCallContext());

        var loginResponse = await service.Login(new BurgerIAM.Protos.Identity.LoginRequest
        {
            Email = "test@test.com",
            Password = "password123"
        }, new MockServerCallContext());

        var validation = await service.ValidateToken(new BurgerIAM.Protos.Identity.ValidateTokenRequest
        {
            Token = loginResponse.Token
        }, new MockServerCallContext());

        Assert.True(validation.IsValid);
        Assert.Equal("Customer", validation.Role);
    }

    [Fact]
    public async Task ValidateToken_InvalidToken_ReturnsInvalid()
    {
        var db = CreateDbContext();
        var service = CreateService(db);

        var validation = await service.ValidateToken(new BurgerIAM.Protos.Identity.ValidateTokenRequest
        {
            Token = "invalid-token"
        }, new MockServerCallContext());

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task ValidateToken_TokenVersionMismatch_ReturnsInvalid()
    {
        var db = CreateDbContext();
        SeedTokenVersion(db);
        var service = CreateService(db);

        await service.Register(new BurgerIAM.Protos.Identity.RegisterRequest
        {
            Email = "test@test.com",
            Password = "password123",
            Name = "Test User"
        }, new MockServerCallContext());

        var loginResponse = await service.Login(new BurgerIAM.Protos.Identity.LoginRequest
        {
            Email = "test@test.com",
            Password = "password123"
        }, new MockServerCallContext());

        var version = db.TokenVersions.First();
        db.TokenVersions.Remove(version);
        db.SaveChanges();

        var newVersion = new TokenVersion
        {
            Version = Guid.NewGuid().ToString("N"),
            GeneratedAt = DateTime.UtcNow
        };
        db.TokenVersions.Add(newVersion);
        db.SaveChanges();

        var validation = await service.ValidateToken(new BurgerIAM.Protos.Identity.ValidateTokenRequest
        {
            Token = loginResponse.Token
        }, new MockServerCallContext());

        Assert.False(validation.IsValid);
    }
}
