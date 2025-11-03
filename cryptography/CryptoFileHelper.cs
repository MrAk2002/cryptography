using System.IO;
using System.Security.Cryptography;

namespace cryptography;

public static class CryptoFileHelper
{
    public enum CipherAlgorithm { Aes, Des, TripleDes }
    public enum CipherModeChoice { Cbc, Ecb }

    // Configurable defaults
    private const int SaltSize = 16;            // bytes
    private const int Pbkdf2Iterations = 100_000;

    #region Public API

    // Encrypt using a password (PBKDF2). Writes: [salt][iv][ciphertext]
    public static void EncryptFileWithPassword(string inputPath, string outputPath,
        CipherAlgorithm alg, CipherModeChoice mode, string password,
        int pbkdf2Iterations = Pbkdf2Iterations)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var cipher = CreateSymmetricAlgorithm(alg, mode);
        int keyLen = cipher.KeySize / 8;
        var pdb = new Rfc2898DeriveBytes(password, salt, pbkdf2Iterations, HashAlgorithmName.SHA256);
        byte[] key = pdb.GetBytes(keyLen);

        // Use the algorithm's default block-size IV length
        byte[] iv = RandomNumberGenerator.GetBytes(cipher.BlockSize / 8);

        EncryptFileStream(inputPath, outputPath, cipher, key, iv, salt);
    }

    // Decrypt using a password (reads salt from file)
    public static void DecryptFileWithPassword(string inputPath, string outputPath,
        CipherAlgorithm alg, CipherModeChoice mode, string password,
        int pbkdf2Iterations = Pbkdf2Iterations)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        using var cipher = CreateSymmetricAlgorithm(alg, mode);
        int keyLen = cipher.KeySize / 8;

        using var inFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        // Read salt
        byte[] salt = ReadExact(inFs, SaltSize);
        // Read IV
        byte[] iv = ReadExact(inFs, cipher.BlockSize / 8);

        var pdb = new Rfc2898DeriveBytes(password, salt, pbkdf2Iterations, HashAlgorithmName.SHA256);
        byte[] key = pdb.GetBytes(keyLen);

        DecryptStreamToFile(inFs, outputPath, cipher, key, iv);
    }

    // Encrypt using a raw key byte[] (user supplied). Still writes salt bytes (zeroed) to keep file format consistent.
    public static void EncryptFileWithKey(string inputPath, string outputPath,
        CipherAlgorithm alg, CipherModeChoice mode, byte[] key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        using var cipher = CreateSymmetricAlgorithm(alg, mode);
        if (key.Length != cipher.KeySize / 8)
            throw new ArgumentException($"Key size mismatch. Expected {cipher.KeySize/8} bytes.");

        byte[] salt = new byte[SaltSize]; // zero salt to indicate raw-key usage (client app can detect if desired)
        byte[] iv = RandomNumberGenerator.GetBytes(cipher.BlockSize / 8);

        EncryptFileStream(inputPath, outputPath, cipher, key, iv, salt);
    }

    // Decrypt using a raw key. Expects file to contain salt + iv + ciphertext; salt will be ignored.
    public static void DecryptFileWithKey(string inputPath, string outputPath,
        CipherAlgorithm alg, CipherModeChoice mode, byte[] key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        using var cipher = CreateSymmetricAlgorithm(alg, mode);
        if (key.Length != cipher.KeySize / 8)
            throw new ArgumentException($"Key size mismatch. Expected {cipher.KeySize/8} bytes.");

        using var inFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        // read and ignore salt
        byte[] salt = ReadExact(inFs, SaltSize);
        byte[] iv = ReadExact(inFs, cipher.BlockSize / 8);

        DecryptStreamToFile(inFs, outputPath, cipher, key, iv);
    }

    // Helper to generate a random key for an algorithm
    public static byte[] GenerateRandomKey(CipherAlgorithm alg)
    {
        using var cipher = CreateSymmetricAlgorithm(alg, CipherModeChoice.Cbc);
        return RandomNumberGenerator.GetBytes(cipher.KeySize / 8);
    }
    
    #endregion

    #region Internals (streaming encryption/decryption)

    private static void EncryptFileStream(string inputPath, string outputPath,
        SymmetricAlgorithm cipher, byte[] key, byte[] iv, byte[] salt)
    {
        cipher.Padding = PaddingMode.PKCS7;
        cipher.Key = key;
        cipher.IV = iv;

        // Open streams
        using var inFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
        using var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);

        // Write header: salt then iv
        outFs.Write(salt, 0, salt.Length);
        outFs.Write(iv, 0, iv.Length);

        using var cryptoTransform = cipher.CreateEncryptor();
        using var cryptoStream = new CryptoStream(outFs, cryptoTransform, CryptoStreamMode.Write);

        // Copy in -> cryptoStream -> out
        inFs.CopyTo(cryptoStream);
        cryptoStream.FlushFinalBlock();
    }

    private static void DecryptStreamToFile(FileStream inFs, string outputPath,
        SymmetricAlgorithm cipher, byte[] key, byte[] iv)
    {
        cipher.Padding = PaddingMode.PKCS7;
        cipher.Key = key;
        cipher.IV = iv;

        using var outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var cryptoTransform = cipher.CreateDecryptor();
        using var cryptoStream = new CryptoStream(inFs, cryptoTransform, CryptoStreamMode.Read);

        cryptoStream.CopyTo(outFs);
    }

    // Creates and configures the SymmetricAlgorithm for the chosen algorithm and mode
    private static SymmetricAlgorithm CreateSymmetricAlgorithm(CipherAlgorithm alg, CipherModeChoice modeChoice)
    {
        SymmetricAlgorithm cipher = alg switch
        {
            CipherAlgorithm.Aes => Aes.Create(),
            CipherAlgorithm.Des => DES.Create(),
            CipherAlgorithm.TripleDes => TripleDES.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(alg), "Unsupported algorithm")
        };

        // set mode
        cipher.Mode = modeChoice == CipherModeChoice.Cbc ? CipherMode.CBC : CipherMode.ECB;
        
        return cipher;
    }

    // Utility to read exact bytes (throws if EOF)
    private static byte[] ReadExact(Stream s, int length)
    {
        byte[] buf = new byte[length];
        int read = 0;
        while (read < length)
        {
            int n = s.Read(buf, read, length - read);
            if (n == 0) throw new EndOfStreamException("Unexpected end of stream while reading header.");
            read += n;
        }
        return buf;
    }

    #endregion
}