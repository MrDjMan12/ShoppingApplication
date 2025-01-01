using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text;
using NuGet.Packaging;

namespace ShoppingApplication.Services;

public interface IJwtTokenService
{
    Task<string> GenerateJwtToken(IdentityUser user);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly UserManager<IdentityUser> _userManager;

    public JwtTokenService(IConfiguration config, ILogger<JwtTokenService> logger, UserManager<IdentityUser> userManager)
    {
        _config = config;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<string> GenerateJwtToken(IdentityUser user)
    {
        var userRoles = await _userManager.GetRolesAsync(user);
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
