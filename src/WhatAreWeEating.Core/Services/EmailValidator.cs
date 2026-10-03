using System.Text.RegularExpressions;
using WhatAreWeEating.Core.Interfaces;

namespace WhatAreWeEating.Core.Services;

public partial class EmailValidator : IEmailValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    public bool IsValid(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (trimmed.Length > 256)
        {
            return false;
        }

        return EmailRegex().IsMatch(trimmed);
    }
}
