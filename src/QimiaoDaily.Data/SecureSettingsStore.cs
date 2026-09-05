using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace QimiaoDaily.Data;

/// Stores user secrets encrypted with Windows DPAPI CurrentUser scope.
/// Secret values never enter SQLite, normal configuration files, or application logs.
[SupportedOSPlatform("windows")]
public sealed class SecureSettingsStore(QimiaoDailyPaths paths)
{
    private static readonly byte[] EntropyPrefix = Encoding.UTF8.GetBytes("QimiaoDaily/secret/v1/");

    public bool Has(string key) => File.Exists(PathFor(key));

    public void Set(string key, string value)
    {
        ValidateKey(key);
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("Secret value cannot be empty.", nameof(value));
        paths.EnsureDirectories();
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy(key), DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(key), encrypted);
    }

    public string? TryGet(string key)
    {
        ValidateKey(key);
        var path = PathFor(key);
        if (!File.Exists(path)) return null;
        try
        {
            var decrypted = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy(key), DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public void Delete(string key)
    {
        ValidateKey(key);
        File.Delete(PathFor(key));
    }

    private string PathFor(string key) => Path.Combine(paths.ConfigDirectory, key + ".dpapi");

    private static byte[] Entropy(string key) => SHA256.HashData([.. EntropyPrefix, .. Encoding.UTF8.GetBytes(key)]);

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '_' && ch != '-'))
            throw new ArgumentException("Secret key contains invalid characters.", nameof(key));
    }
}
