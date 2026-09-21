using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MelonLoader.Installer.Core.Utilities.Signing
{
    /// <summary>
    /// Aligns and signs an APK (signature scheme V2). Based on the LemonLoader installer's signer,
    /// with the crypto replaced by plain managed code (see Sha256Compat.cs).
    /// </summary>
    public class APKSigner
    {
        private readonly SigningCredentials _credentials;
        private readonly Sha256Compat _sha = Sha256Compat.Create();
        private readonly IPatchLogger _logger;

        public APKSigner(string pemData, IPatchLogger patchLogger)
        {
            _logger = patchLogger;

            _logger.Log("Reading certificates");
            _credentials = SigningCredentials.FromPem(pemData);
        }

        public void Sign(string apkPath)
        {
            _logger.Log("Aligning");
            APKAligner.AlignApk(apkPath);

            _logger.Log("Signing with V2");
            SignV2(apkPath);

            _logger.Log("Done");
        }

        #region V2

        private void SignV2(string path)
        {
            FileStream fs = new(path, FileMode.Open);
            using FileMemory memory = new(fs);
            using MemoryStream ms = new();
            using FileMemory outMemory = new(ms);
            memory.Position = memory.Length() - 22;
            while (memory.ReadInt() != EndOfCentralDirectory.SIGNATURE)
            {
                memory.Position -= 4 + 1;
            }
            memory.Position -= 4;
            var eocdPosition = memory.Position;
            EndOfCentralDirectory eocd = new(memory);
            if (eocd == null)
                return;
            var cd = eocd.OffsetOfCD;
            memory.Position = cd - 16 - 8;
            var d = memory.ReadULong();
            var d2 = memory.ReadString(16);
            var section1 = GetSectionDigests(fs, 0, cd);
            var section3 = GetSectionDigests(fs, cd, eocdPosition);
            var section4 = GetSectionDigests(fs, eocdPosition, fs.Length);

            var digestChunks = section1.Concat(section3).Concat(section4).ToList();

            byte[] bytes = new byte[1 + 4];
            bytes[0] = 0x5a;
            byte[] sizeBytes = BitConverter.GetBytes((uint)digestChunks.Count);
            bytes[1] = sizeBytes[0];
            bytes[2] = sizeBytes[1];
            bytes[3] = sizeBytes[2];
            bytes[4] = sizeBytes[3];
            var digest = _sha.ComputeHash([.. bytes, .. digestChunks.Aggregate((a, b) => a.Concat(b).ToArray())]);

            uint algorithm = 0x0103;

            APKSignatureSchemeV2 block = new();
            APKSignatureSchemeV2.Signer signer = new();

            using MemoryStream signedDataMs = new();
            using FileMemory memorySignedData = new(signedDataMs);
            var signedData = new APKSignatureSchemeV2.Signer.BlockSignedData();
            signedData.Digests.Add(new APKSignatureSchemeV2.Signer.BlockSignedData.Digest(algorithm, digest));

            signedData.Certificates.Add(_credentials.CertificateDer);

            signedData.Write(memorySignedData);
            signer.SignedData = signedDataMs.ToArray();

            signer.Signatures.Add(new APKSignatureSchemeV2.Signer.BlockSignature(algorithm, _credentials.SignSha256(signer.SignedData)));
            signer.PublicKey = _credentials.PublicKeyInfoDer;
            block.Signers.Add(signer);

            APKSigningBlock signingBlock = new();
            signingBlock.Values.Add(block.ToIDValuePair());

            fs.Position = 0;
            outMemory.WriteBytes(memory.ReadBytes(cd));
            signingBlock.Write(outMemory);
            eocd.OffsetOfCD = (int)ms.Position;
            outMemory.WriteBytes(memory.ReadBytes((int)(eocdPosition - cd)));
            eocd.Write(outMemory);

            fs.SetLength(0);
            ms.Position = 0;
            ms.CopyTo(fs);
            fs.Close();
        }

        private List<byte[]> GetSectionDigests(FileStream fs, long startOffset, long endOffset)
        {
            var digests = new List<byte[]>();
            int chunkSize = 1024 * 1024;
            for (long i = startOffset; i < endOffset; i += chunkSize)
            {
                fs.Position = i;
                var size = Math.Min(endOffset - i, chunkSize);
                byte[] bytes = new byte[1 + 4 + size];
                bytes[0] = 0xa5;
                byte[] sizeBytes = BitConverter.GetBytes((uint)size);
                bytes[1] = sizeBytes[0];
                bytes[2] = sizeBytes[1];
                bytes[3] = sizeBytes[2];
                bytes[4] = sizeBytes[3];
                fs.Read(bytes, 5, (int)size);
                digests.Add(_sha.ComputeHash(bytes));
            }
            return digests;
        }

        #endregion
    }
}
