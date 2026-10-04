using System.Security.Cryptography;
using System.Text;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Core.Services;

public class TokenService : ITokenService
{
    public (string RawToken, string TokenHash) GenerateToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        // Codificación Base64Url (sin caracteres +, / ni padding =)
        string rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        string tokenHash = HashToken(rawToken);

        return (rawToken, tokenHash);
    }

    public string HashToken(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(rawToken);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}
