using Fundoo.User.Domain.Entities;
using Fundoo.User.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Fundoo.User.UnitTests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_IncludesTheAssociatedSessionId()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var service = new JwtTokenService(new JwtOptions
        {
            Issuer = "fundoo-user-api",
            Audience = "fundoo-clients",
            SigningKey = "0123456789abcdef0123456789abcdef",
            ExpiryMinutes = 30
        });

        var token = service.CreateAccessToken(new AppUser
        {
            Id = userId,
            Email = "asha@example.com"
        }, sessionId);
        var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.Equal(userId.ToString(), parsed.Claims.Single(claim => claim.Type == "sub").Value);
        Assert.Equal(sessionId.ToString(), parsed.Claims.Single(claim => claim.Type == "sid").Value);
    }
}
