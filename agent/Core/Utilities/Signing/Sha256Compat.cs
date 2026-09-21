using Org.BouncyCastle.Crypto.Digests;
using System.IO;

namespace MelonLoader.Installer.Core.Utilities.Signing;

/// <summary>
/// SHA-256 built on BouncyCastle, so signing never needs the operating system's crypto library
/// (which isn't available to .NET programs running directly on Android).
/// </summary>
internal sealed class Sha256Compat
{
    public static Sha256Compat Create() => new();

    public byte[] ComputeHash(byte[] data)
    {
        Sha256Digest digest = new();
        digest.BlockUpdate(data, 0, data.Length);
        return Finish(digest);
    }

    public byte[] ComputeHash(Stream stream)
    {
        Sha256Digest digest = new();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            digest.BlockUpdate(buffer, 0, read);
        return Finish(digest);
    }

    private static byte[] Finish(Sha256Digest digest)
    {
        byte[] output = new byte[digest.GetDigestSize()];
        digest.DoFinal(output, 0);
        return output;
    }
}
