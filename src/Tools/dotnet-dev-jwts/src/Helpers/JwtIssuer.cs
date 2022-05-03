// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.AspNetCore.Authentication.JwtBearer.Tools;

public class JwtIssuer
{
    private readonly SymmetricSecurityKey _signingKey;

    public JwtIssuer(string issuer, byte[] signingKeyMaterial)
    {
        Issuer = issuer;
        _signingKey = new SymmetricSecurityKey(signingKeyMaterial);
    }

    public string Issuer { get; }

    public JwtSecurityToken Create(
        string name,
        string audience,
        DateTime notBefore,
        DateTime expires,
        DateTime issuedAt,
        IEnumerable<string> scopes = null,
        IEnumerable<string> roles = null,
        IDictionary<string, string> claims = null)
    {
        var identity = new GenericIdentity(name);

        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, name));

        var id = Guid.NewGuid().ToString().GetHashCode().ToString("x", CultureInfo.InvariantCulture);
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, id));

        if (scopes is { } scopesToAdd)
        {
            identity.AddClaims(scopesToAdd.Select(s => new Claim("scope", s)));
        }

        if (roles is { } rolesToAdd)
        {
            identity.AddClaims(rolesToAdd.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        if (claims is { Count: > 0 } claimsToAdd)
        {
            identity.AddClaims(claimsToAdd.Select(kvp => new Claim(kvp.Key, kvp.Value)));
        }

        var handler = new JwtSecurityTokenHandler();
        var jwtSigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature);
        var jwtToken = handler.CreateJwtSecurityToken(Issuer, audience, identity, notBefore, expires, issuedAt, jwtSigningCredentials);
        return jwtToken;
    }

    public string WriteToken(JwtSecurityToken token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(token);
    }

    public JwtSecurityToken Extract(string token) => new JwtSecurityToken(token);

    public bool IsValid(string encodedToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = _signingKey,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true
        };
        if (handler.ValidateToken(encodedToken, tokenValidationParameters, out _).Identity?.IsAuthenticated == true)
        {
            return true;
        }
        return false;
    }
}
