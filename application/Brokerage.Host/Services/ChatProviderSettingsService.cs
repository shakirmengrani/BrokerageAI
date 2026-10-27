using System.Security.Cryptography;
using System.Text;
using Brokerage.Identity;
using Microsoft.EntityFrameworkCore;

namespace Brokerage.Host.Services;

public sealed record ChatProviderSettingsSnapshot(
    string? Endpoint,
    string? Model,
    string? ApiKey,
    bool HasApiKey);

public sealed class ChatProviderSettingsService
{
    private readonly IdentityDb _db;
    private readonly IConfiguration _configuration;
    private readonly byte[] _encryptionKey;

    public ChatProviderSettingsService(IdentityDb db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
        var configuredKey = configuration["Chat:SettingsEncryptionKey"];
        try
        {
            _encryptionKey = Convert.FromBase64String(configuredKey ?? string.Empty);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Chat:SettingsEncryptionKey must be a Base64-encoded 32-byte key.",
                exception);
        }

        if (_encryptionKey.Length != 32)
        {
            throw new InvalidOperationException(
                "Chat:SettingsEncryptionKey must be a Base64-encoded 32-byte key.");
        }
    }

    public async Task<ChatProviderSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var savedSettings = await _db.ChatProviderSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(settings => settings.Id == 1, cancellationToken);
        var encryptedKey = savedSettings?.EncryptedApiKey;
        var apiKey = encryptedKey is null
            ? _configuration["Chat:ApiKey"]
            : Decrypt(encryptedKey);
        return new ChatProviderSettingsSnapshot(
            savedSettings?.Endpoint ?? _configuration["Chat:Endpoint"],
            savedSettings?.Model ?? _configuration["Chat:Model"],
            apiKey,
            !string.IsNullOrWhiteSpace(apiKey));
    }

    public async Task SaveAsync(
        string endpoint,
        string model,
        string? apiKey,
        Guid updatedByUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEndpoint = endpoint.Trim();
        if (!Uri.TryCreate(normalizedEndpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("Enter a valid HTTP or HTTPS chat completions URL.", nameof(endpoint));
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
        {
            throw new ArgumentException(
                "The chat provider endpoint must use HTTPS unless it is a loopback development endpoint.",
                nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(model) || model.Trim().Length > 200)
            throw new ArgumentException("Model must contain 1 to 200 characters.", nameof(model));

        var savedSettings = await _db.ChatProviderSettings
            .SingleOrDefaultAsync(settings => settings.Id == 1, cancellationToken);
        if (savedSettings is null)
        {
            savedSettings = new ChatProviderSettings
            {
                Id = 1,
                Endpoint = normalizedEndpoint,
                Model = model.Trim(),
                EncryptedApiKey = null,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedByUserId = updatedByUserId
            };
            _db.ChatProviderSettings.Add(savedSettings);
        }
        else
        {
            savedSettings.Endpoint = normalizedEndpoint;
            savedSettings.Model = model.Trim();
            savedSettings.UpdatedAt = DateTimeOffset.UtcNow;
            savedSettings.UpdatedByUserId = updatedByUserId;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
            savedSettings.EncryptedApiKey = Encrypt(apiKey.Trim());

        await _db.SaveChangesAsync(cancellationToken);
    }

    private string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_encryptionKey, tag.Length))
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var envelope = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(envelope, 0);
        tag.CopyTo(envelope, nonce.Length);
        ciphertext.CopyTo(envelope, nonce.Length + tag.Length);
        CryptographicOperations.ZeroMemory(plaintextBytes);
        return Convert.ToBase64String(envelope);
    }

    private string Decrypt(string encryptedValue)
    {
        byte[] envelope;
        try
        {
            envelope = Convert.FromBase64String(encryptedValue);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("The saved chat provider secret is invalid.", exception);
        }

        const int nonceLength = 12;
        const int tagLength = 16;
        if (envelope.Length < nonceLength + tagLength)
            throw new InvalidOperationException("The saved chat provider secret is invalid.");

        var plaintext = new byte[envelope.Length - nonceLength - tagLength];
        try
        {
            using var aes = new AesGcm(_encryptionKey, tagLength);
            aes.Decrypt(
                envelope.AsSpan(0, nonceLength),
                envelope.AsSpan(nonceLength + tagLength),
                envelope.AsSpan(nonceLength, tagLength),
                plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                "Could not decrypt the saved chat provider secret. Check Chat:SettingsEncryptionKey.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
