using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Grpc.Core;
using IdentityService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtoIdentity = BurgerIAM.Protos.Identity;

namespace IdentityService.Services;

public sealed class IdentityGrpcService : ProtoIdentity.IdentityService.IdentityServiceBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public IdentityGrpcService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public override async Task<ProtoIdentity.LoginResponse> Login(ProtoIdentity.LoginRequest request, ServerCallContext context)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, context.CancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new ProtoIdentity.LoginResponse { Error = "Invalid email or password" };
        }

        var tokenVersion = await _db.TokenVersions.FirstOrDefaultAsync(context.CancellationToken);
        var version = tokenVersion?.Version ?? Guid.NewGuid().ToString("N");

        var token = GenerateJwtToken(user, version);

        return new ProtoIdentity.LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role
        };
    }

    public override async Task<ProtoIdentity.RegisterResponse> Register(ProtoIdentity.RegisterRequest request, ServerCallContext context)
    {
        var existing = await _db.Users.AnyAsync(u => u.Email == request.Email, context.CancellationToken);
        if (existing)
        {
            return new ProtoIdentity.RegisterResponse { Error = "Email already registered" };
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Name = request.Name,
            Role = "Customer"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new ProtoIdentity.RegisterResponse { UserId = user.Id };
    }

    public override async Task<ProtoIdentity.ValidateTokenResponse> ValidateToken(ProtoIdentity.ValidateTokenRequest request, ServerCallContext context)
    {
        try
        {
            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
            var handler = new JwtSecurityTokenHandler();
            var result = handler.ValidateToken(request.Token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var tokenVersionClaim = result.FindFirst("token_version")?.Value;
            if (tokenVersionClaim is null)
                return new ProtoIdentity.ValidateTokenResponse { IsValid = false };

            var currentVersion = await _db.TokenVersions.FirstOrDefaultAsync(context.CancellationToken);
            if (currentVersion is null || tokenVersionClaim != currentVersion.Version)
                return new ProtoIdentity.ValidateTokenResponse { IsValid = false };

            var userId = result.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = result.FindFirst(ClaimTypes.Role)?.Value;

            return new ProtoIdentity.ValidateTokenResponse
            {
                IsValid = true,
                UserId = userId ?? string.Empty,
                Role = role ?? string.Empty
            };
        }
        catch
        {
            return new ProtoIdentity.ValidateTokenResponse { IsValid = false };
        }
    }

    private string GenerateJwtToken(User user, string tokenVersion)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("token_version", tokenVersion)
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
