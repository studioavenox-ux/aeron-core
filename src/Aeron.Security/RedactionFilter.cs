using System.Text.RegularExpressions;
using Aeron.Contracts;

namespace Aeron.Security;

/// <summary>
/// Thread-safe, unconditional secret redaction engine for AERON logs, errors, and diagnostics.
/// Ensures zero raw credentials, API keys, tokens, passwords, private keys, or registered
/// secret values ever reach persistent logs or UI diagnostics.
/// </summary>
public sealed class RedactionFilter : IRedactionFilter
{
    public const string RedactedPlaceholder = "[REDACTED]";
    public const int MinimumRegisteredSecretLength = 4;

    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(2);

    private static readonly RegexOptions SafeOptions =
        RegexOptions.Compiled | RegexOptions.CultureInvariant;

    private static readonly Regex PemBlockRegex = new(
        @"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z0-9 ]*PRIVATE KEY-----",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex AuthHeaderRegex = new(
        @"(?i)(\b(?:Authorization|Proxy-Authorization)\s*[:=]\s*(?:Bearer|Basic|Token|ApiKey|Digest)\s+)([^\s,;""'\r\n]+)",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex StandaloneBearerRegex = new(
        @"(?i)(\bBearer\s+)([A-Za-z0-9\-._~+/]{8,}=*)",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex JwtRegex = new(
        @"\beyJ[A-Za-z0-9_\-]{8,}\.eyJ[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\b",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex KnownApiKeyPrefixRegex = new(
        @"\b(?:sk-or-v1-[A-Za-z0-9_\-]{12,}|sk-proj-[A-Za-z0-9_\-]{12,}|sk-ant-[A-Za-z0-9_\-]{12,}|sk-[A-Za-z0-9_\-]{12,}|pk-[A-Za-z0-9_\-]{12,}|ghp_[A-Za-z0-9]{16,}|gho_[A-Za-z0-9]{16,}|ghu_[A-Za-z0-9]{16,}|ghs_[A-Za-z0-9]{16,}|github_pat_[A-Za-z0-9_]{16,}|xox[baprs]-[A-Za-z0-9\-]{12,}|AIza[0-9A-Za-z\-_]{20,}|AKIA[0-9A-Z]{16}|ya29\.[0-9A-Za-z\-_]{16,}|glpat-[A-Za-z0-9\-_]{12,})\b",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex UrlUserInfoRegex = new(
        @"([A-Za-z][A-Za-z0-9+.\-]*://[^/\s:@""']+:)(?!\[REDACTED\])([^@\s/""']+)(@)",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex JsonSecretFieldRegex = new(
        @"(?i)(""(?:password|passwd|pwd|secret|client_secret|clientsecret|api_key|apikey|api-key|access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|auth_token|authtoken|private_key|privatekey|token|credential|credentials|account_key|accountkey|shared_access_key|sharedaccesskey|omniroute_api_key|x-api-key)""\s*:\s*"")(?!\[REDACTED\]"")((?:\\.|[^""\\])*)("")",
        SafeOptions,
        PatternTimeout);

    private static readonly Regex KeyValueSecretRegex = new(
        @"(?i)(\b(?:password|passwd|pwd|secret|client_secret|clientsecret|api_key|apikey|api-key|access_token|accesstoken|refresh_token|refreshtoken|id_token|idtoken|auth_token|authtoken|private_key|privatekey|token|credential|account_key|accountkey|shared_access_key|sharedaccesskey|omniroute_api_key|x-api-key)\s*[:=]\s*)(?!\[REDACTED\])(?:""[^""\r\n]*""|'[^'\r\n]*'|[^\s,;&""'\r\n]+)",
        SafeOptions,
        PatternTimeout);

    private static readonly string[] NormalizedSensitiveKeyTerms =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "apikey",
        "token",
        "privatekey",
        "credential",
        "credentials",
        "authorization",
        "proxyauthorization",
        "bearer",
        "accountkey",
        "sharedaccesskey",
        "connectionstring"
    ];

    private readonly Lock _sync = new();
    private readonly HashSet<string> _registeredSet = new(StringComparer.Ordinal);
    private string[] _orderedSecrets = [];

    public static bool IsSensitiveKeyName(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = NormalizeKey(key);
        foreach (var term in NormalizedSensitiveKeyTerms)
        {
            if (normalized.Contains(term, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void RegisterSensitiveValue(string sensitiveValue)
    {
        if (string.IsNullOrWhiteSpace(sensitiveValue))
        {
            return;
        }

        var trimmed = sensitiveValue.Trim();
        if (trimmed.Length < MinimumRegisteredSecretLength ||
            string.Equals(trimmed, RedactedPlaceholder, StringComparison.Ordinal))
        {
            return;
        }

        lock (_sync)
        {
            if (_registeredSet.Add(trimmed))
            {
                _orderedSecrets = _registeredSet
                    .OrderByDescending(static s => s.Length)
                    .ThenBy(static s => s, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    public string Redact(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        try
        {
            var current = ReplaceRegisteredSecrets(input);

            current = PemBlockRegex.Replace(current, RedactedPlaceholder);
            current = JsonSecretFieldRegex.Replace(current, $"$1{RedactedPlaceholder}$3");
            current = AuthHeaderRegex.Replace(current, static match =>
                string.Equals(match.Groups[2].Value, RedactedPlaceholder, StringComparison.Ordinal)
                    ? match.Value
                    : $"{match.Groups[1].Value}{RedactedPlaceholder}");
            current = StandaloneBearerRegex.Replace(current, $"$1{RedactedPlaceholder}");
            current = UrlUserInfoRegex.Replace(current, $"$1{RedactedPlaceholder}$3");
            current = JwtRegex.Replace(current, RedactedPlaceholder);
            current = KnownApiKeyPrefixRegex.Replace(current, RedactedPlaceholder);
            current = KeyValueSecretRegex.Replace(current, $"$1{RedactedPlaceholder}");

            return ReplaceRegisteredSecrets(current);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: never return unredacted input on pattern timeout.
            return RedactedPlaceholder;
        }
    }

    public bool ContainsPotentialSecret(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        try
        {
            var secrets = Volatile.Read(ref _orderedSecrets);
            foreach (var secret in secrets)
            {
                if (input.Contains(secret, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            if (PemBlockRegex.IsMatch(input) ||
                JwtRegex.IsMatch(input) ||
                KnownApiKeyPrefixRegex.IsMatch(input) ||
                UrlUserInfoRegex.IsMatch(input) ||
                StandaloneBearerRegex.IsMatch(input) ||
                JsonSecretFieldRegex.IsMatch(input) ||
                KeyValueSecretRegex.IsMatch(input))
            {
                return true;
            }

            var authMatch = AuthHeaderRegex.Match(input);
            return authMatch.Success &&
                   !string.Equals(authMatch.Groups[2].Value, RedactedPlaceholder, StringComparison.Ordinal);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed on pathological input.
            return true;
        }
    }

    private string ReplaceRegisteredSecrets(string value)
    {
        var secrets = Volatile.Read(ref _orderedSecrets);
        if (secrets.Length == 0)
        {
            return value;
        }

        var result = value;
        foreach (var secret in secrets)
        {
            if (result.Contains(secret, StringComparison.Ordinal))
            {
                result = result.Replace(secret, RedactedPlaceholder, StringComparison.Ordinal);
            }
        }

        return result;
    }

    private static string NormalizeKey(string key)
    {
        Span<char> buffer = key.Length <= 128 ? stackalloc char[key.Length] : new char[key.Length];
        var count = 0;

        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[count++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer[..count]);
    }
}
