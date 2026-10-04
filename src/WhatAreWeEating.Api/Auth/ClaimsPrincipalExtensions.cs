using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace WhatAreWeEating.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static bool TryGetSesionId(this ClaimsPrincipal principal, out Guid sesionId)
        => Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sid)?.Value, out sesionId);

    public static bool TryGetUsuarioId(this ClaimsPrincipal principal, out Guid usuarioId)
        => Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out usuarioId);
}
