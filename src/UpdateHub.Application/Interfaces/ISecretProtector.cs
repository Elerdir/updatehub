namespace UpdateHub.Application.Interfaces;

/// <summary>
/// Encrypts/decrypts small secrets (e.g. the TOTP shared secret) before they are
/// persisted, so a database leak alone does not expose them.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
