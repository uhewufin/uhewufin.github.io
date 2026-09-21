using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace MelonLoader.Installer.Core.Utilities.Signing;

// Everything the signer needs, written in plain managed code.
// BouncyCastle was dropped because parts of it use reflection that an AOT build can't run,
// and .NET's own crypto needs OpenSSL, which Android doesn't have.

/// <summary>SHA-256 (FIPS 180-4).</summary>
internal sealed class Sha256Compat
{
    private static readonly uint[] K =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    public static Sha256Compat Create() => new();

    public byte[] ComputeHash(byte[] data)
    {
        State state = new();
        state.Update(data, 0, data.Length);
        return state.Finish();
    }

    public byte[] ComputeHash(Stream stream)
    {
        State state = new();
        byte[] buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            state.Update(buffer, 0, read);
        return state.Finish();
    }

    private sealed class State
    {
        private readonly uint[] _h =
        [
            0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19,
        ];
        private readonly byte[] _block = new byte[64];
        private readonly uint[] _w = new uint[64];
        private int _blockLength;
        private long _totalLength;

        public void Update(byte[] data, int offset, int count)
        {
            _totalLength += count;
            while (count > 0)
            {
                int take = Math.Min(64 - _blockLength, count);
                Buffer.BlockCopy(data, offset, _block, _blockLength, take);
                _blockLength += take;
                offset += take;
                count -= take;

                if (_blockLength == 64)
                {
                    Process();
                    _blockLength = 0;
                }
            }
        }

        public byte[] Finish()
        {
            long bitLength = _totalLength * 8;

            _block[_blockLength++] = 0x80;
            if (_blockLength > 56)
            {
                while (_blockLength < 64)
                    _block[_blockLength++] = 0;
                Process();
                _blockLength = 0;
            }

            while (_blockLength < 56)
                _block[_blockLength++] = 0;
            for (int i = 7; i >= 0; i--)
                _block[_blockLength++] = (byte)(bitLength >> (i * 8));
            Process();

            byte[] result = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                result[i * 4] = (byte)(_h[i] >> 24);
                result[i * 4 + 1] = (byte)(_h[i] >> 16);
                result[i * 4 + 2] = (byte)(_h[i] >> 8);
                result[i * 4 + 3] = (byte)_h[i];
            }
            return result;
        }

        private void Process()
        {
            uint[] w = _w;
            for (int i = 0; i < 16; i++)
            {
                w[i] = ((uint)_block[i * 4] << 24) | ((uint)_block[i * 4 + 1] << 16)
                     | ((uint)_block[i * 4 + 2] << 8) | _block[i * 4 + 3];
            }

            for (int t = 16; t < 64; t++)
            {
                uint s0 = BitOperations.RotateRight(w[t - 15], 7) ^ BitOperations.RotateRight(w[t - 15], 18) ^ (w[t - 15] >> 3);
                uint s1 = BitOperations.RotateRight(w[t - 2], 17) ^ BitOperations.RotateRight(w[t - 2], 19) ^ (w[t - 2] >> 10);
                w[t] = w[t - 16] + s0 + w[t - 7] + s1;
            }

            uint a = _h[0], b = _h[1], c = _h[2], d = _h[3], e = _h[4], f = _h[5], g = _h[6], h = _h[7];

            for (int t = 0; t < 64; t++)
            {
                uint bigS1 = BitOperations.RotateRight(e, 6) ^ BitOperations.RotateRight(e, 11) ^ BitOperations.RotateRight(e, 25);
                uint ch = (e & f) ^ (~e & g);
                uint temp1 = h + bigS1 + ch + K[t] + w[t];
                uint bigS0 = BitOperations.RotateRight(a, 2) ^ BitOperations.RotateRight(a, 13) ^ BitOperations.RotateRight(a, 22);
                uint maj = (a & b) ^ (a & c) ^ (b & c);
                uint temp2 = bigS0 + maj;

                h = g;
                g = f;
                f = e;
                e = d + temp1;
                d = c;
                c = b;
                b = a;
                a = temp1 + temp2;
            }

            _h[0] += a; _h[1] += b; _h[2] += c; _h[3] += d;
            _h[4] += e; _h[5] += f; _h[6] += g; _h[7] += h;
        }
    }
}

/// <summary>
/// A certificate plus its RSA private key, read from PEM text, able to sign with RSASSA-PKCS1-v1_5 and SHA-256.
/// </summary>
internal sealed class SigningCredentials
{
    // DER prefix of a SHA-256 DigestInfo (RFC 8017, section 9.2).
    private static readonly byte[] DigestInfoPrefix =
    [
        0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20,
    ];

    private readonly BigInteger _modulus;
    private readonly BigInteger _privateExponent;
    private readonly int _keyLength;

    /// <summary>The certificate, DER encoded.</summary>
    public byte[] CertificateDer { get; }

    /// <summary>The certificate's SubjectPublicKeyInfo, DER encoded.</summary>
    public byte[] PublicKeyInfoDer { get; }

    private SigningCredentials(byte[] certificateDer, byte[] publicKeyInfoDer, BigInteger modulus, BigInteger privateExponent)
    {
        CertificateDer = certificateDer;
        PublicKeyInfoDer = publicKeyInfoDer;
        _modulus = modulus;
        _privateExponent = privateExponent;
        _keyLength = (int)((modulus.GetBitLength() + 7) / 8);
    }

    public static SigningCredentials FromPem(string pem)
    {
        byte[]? certificate = null;
        byte[]? privateKey = null;

        foreach ((string label, byte[] der) in ReadPemBlocks(pem))
        {
            if (label == "CERTIFICATE")
                certificate ??= der;
            else if (label == "RSA PRIVATE KEY")
                privateKey ??= der;
        }

        if (certificate == null)
            throw new InvalidDataException("Certificate could not be loaded from PEM data.");
        if (privateKey == null)
            throw new InvalidDataException("Private key could not be loaded from PEM data.");

        // RSAPrivateKey ::= SEQUENCE { version, modulus, publicExponent, privateExponent, ... }
        ReadTlv(privateKey, 0, out int keyStart, out _);
        int pos = keyStart;
        ReadInteger(privateKey, ref pos); // version
        BigInteger modulus = ReadInteger(privateKey, ref pos);
        ReadInteger(privateKey, ref pos); // public exponent
        BigInteger privateExponent = ReadInteger(privateKey, ref pos);

        return new SigningCredentials(certificate, ExtractPublicKeyInfo(certificate), modulus, privateExponent);
    }

    /// <summary>Signs data with RSASSA-PKCS1-v1_5 using SHA-256.</summary>
    public byte[] SignSha256(byte[] data)
    {
        byte[] hash = Sha256Compat.Create().ComputeHash(data);

        int paddingLength = _keyLength - 3 - DigestInfoPrefix.Length - hash.Length;
        if (paddingLength < 8)
            throw new InvalidDataException("The signing key is too small.");

        // EM = 0x00 || 0x01 || FF..FF || 0x00 || DigestInfo
        byte[] encoded = new byte[_keyLength];
        encoded[1] = 0x01;
        for (int i = 0; i < paddingLength; i++)
            encoded[2 + i] = 0xFF;
        Buffer.BlockCopy(DigestInfoPrefix, 0, encoded, 3 + paddingLength, DigestInfoPrefix.Length);
        Buffer.BlockCopy(hash, 0, encoded, 3 + paddingLength + DigestInfoPrefix.Length, hash.Length);

        BigInteger message = new(encoded, isUnsigned: true, isBigEndian: true);
        BigInteger signed = BigInteger.ModPow(message, _privateExponent, _modulus);

        byte[] raw = signed.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] signature = new byte[_keyLength];
        Buffer.BlockCopy(raw, 0, signature, _keyLength - raw.Length, raw.Length);
        return signature;
    }

    private static List<(string Label, byte[] Der)> ReadPemBlocks(string pem)
    {
        List<(string Label, byte[] Der)> blocks = new();
        int pos = 0;

        while (true)
        {
            int begin = pem.IndexOf("-----BEGIN ", pos, StringComparison.Ordinal);
            if (begin < 0)
                break;

            int labelStart = begin + "-----BEGIN ".Length;
            int labelEnd = pem.IndexOf("-----", labelStart, StringComparison.Ordinal);
            if (labelEnd < 0)
                break;

            string label = pem.Substring(labelStart, labelEnd - labelStart);
            int bodyStart = labelEnd + 5;
            string endMarker = "-----END " + label + "-----";
            int end = pem.IndexOf(endMarker, bodyStart, StringComparison.Ordinal);
            if (end < 0)
                break;

            blocks.Add((label, Convert.FromBase64String(pem.Substring(bodyStart, end - bodyStart))));
            pos = end + endMarker.Length;
        }

        return blocks;
    }

    // Reads the header of the DER element at pos (single-byte tags only).
    private static void ReadTlv(byte[] data, int pos, out int contentStart, out int contentLength)
    {
        int p = pos + 1;
        int length = data[p++];
        if ((length & 0x80) != 0)
        {
            int count = length & 0x7F;
            length = 0;
            for (int i = 0; i < count; i++)
                length = (length << 8) | data[p++];
        }

        contentStart = p;
        contentLength = length;
    }

    private static BigInteger ReadInteger(byte[] data, ref int pos)
    {
        ReadTlv(data, pos, out int start, out int length);
        pos = start + length;
        return new BigInteger(new ReadOnlySpan<byte>(data, start, length), isUnsigned: true, isBigEndian: true);
    }

    // Certificate ::= SEQUENCE { tbsCertificate SEQUENCE { [0] version, serial, signatureAlgorithm,
    //                            issuer, validity, subject, subjectPublicKeyInfo, ... }, ... }
    private static byte[] ExtractPublicKeyInfo(byte[] certificate)
    {
        ReadTlv(certificate, 0, out int certStart, out _);
        ReadTlv(certificate, certStart, out int tbsStart, out _);

        int pos = tbsStart;
        if (certificate[pos] == 0xA0)
        {
            ReadTlv(certificate, pos, out int versionStart, out int versionLength);
            pos = versionStart + versionLength;
        }

        // Skip serial number, signature algorithm, issuer, validity and subject.
        for (int i = 0; i < 5; i++)
        {
            ReadTlv(certificate, pos, out int fieldStart, out int fieldLength);
            pos = fieldStart + fieldLength;
        }

        ReadTlv(certificate, pos, out int keyInfoStart, out int keyInfoLength);
        int total = keyInfoStart - pos + keyInfoLength;

        byte[] keyInfo = new byte[total];
        Buffer.BlockCopy(certificate, pos, keyInfo, 0, total);
        return keyInfo;
    }
}
