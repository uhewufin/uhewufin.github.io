using System;
using System.IO;
using System.IO.Compression;

namespace MelonLoader.Installer.Core.PatchSteps;

internal class InjectGadgetDependency : IPatchStep
{
    private const string TargetEntry = "lib/arm64-v8a/libil2cpp.so";
    private const string GadgetName = "libfrida-gadget.so";
    private const int Page = 0x1000;

    public bool Run(Patcher patcher)
    {
        string apkPath = patcher.Args.IsSplit ? patcher.Info.OutputLibApkPath! : patcher.Info.OutputBaseApkPath;

        using FileStream zipStream = new(apkPath, FileMode.Open);
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Update);

        ZipArchiveEntry? entry = archive.GetEntry(TargetEntry);
        if (entry == null)
        {
            patcher.Logger.Log($"Couldn't find {TargetEntry} in the APK to patch.");
            return false;
        }

        byte[] original;
        using (Stream entryStream = entry.Open())
        using (MemoryStream ms = new())
        {
            entryStream.CopyTo(ms);
            original = ms.ToArray();
        }

        byte[] patched = AddNeededDependency(original, GadgetName, patcher.Logger);

        entry.Delete();
        ZipArchiveEntry newEntry = archive.CreateEntry(TargetEntry, CompressionLevel.NoCompression);
        using (Stream newEntryStream = newEntry.Open())
        {
            newEntryStream.Write(patched, 0, patched.Length);
        }

        patcher.Logger.Log($"Added {GadgetName} as a dependency of {TargetEntry}");
        return true;
    }

    private static ulong ReadU64(byte[] d, long o) => BitConverter.ToUInt64(d, (int)o);
    private static uint ReadU32(byte[] d, long o) => BitConverter.ToUInt32(d, (int)o);
    private static ushort ReadU16(byte[] d, long o) => BitConverter.ToUInt16(d, (int)o);

    private static void WriteU64(byte[] d, long o, ulong v) => BitConverter.GetBytes(v).CopyTo(d, o);
    private static void WriteU32(byte[] d, long o, uint v) => BitConverter.GetBytes(v).CopyTo(d, o);
    private static void WriteU16(byte[] d, long o, ushort v) => BitConverter.GetBytes(v).CopyTo(d, o);

    public static byte[] AddNeededDependency(byte[] data, string newLibName, IPatchLogger logger)
    {
        if (data.Length < 64 || data[0] != 0x7f || data[1] != (byte)'E' || data[2] != (byte)'L' || data[3] != (byte)'F')
            throw new InvalidDataException("Not an ELF file.");
        if (data[4] != 2)
            throw new InvalidDataException("Only 64-bit ELF is supported.");

        long ePhoff = (long)ReadU64(data, 32);
        long eShoff = (long)ReadU64(data, 40);
        int ePhentsize = ReadU16(data, 54);
        int ePhnum = ReadU16(data, 56);
        int eShentsize = ReadU16(data, 58);
        int eShnum = ReadU16(data, 60);

        const uint PT_DYNAMIC = 2, PT_LOAD = 1;
        int dynPhIndex = -1;
        ulong maxVaddrEnd = 0;

        for (int i = 0; i < ePhnum; i++)
        {
            long off = ePhoff + i * ePhentsize;
            uint pType = ReadU32(data, off);
            ulong pVaddr = ReadU64(data, off + 16);
            ulong pMemsz = ReadU64(data, off + 40);
            if (pType == PT_DYNAMIC) dynPhIndex = i;
            if (pType == PT_LOAD) maxVaddrEnd = Math.Max(maxVaddrEnd, pVaddr + pMemsz);
        }
        if (dynPhIndex < 0) throw new InvalidDataException("No PT_DYNAMIC segment found.");

        long dynPhOff = ePhoff + dynPhIndex * ePhentsize;
        ulong oldDynVaddr = ReadU64(data, dynPhOff + 16);
        long oldDynOffset = (long)ReadU64(data, dynPhOff + 8);
        long oldDynFilesz = (long)ReadU64(data, dynPhOff + 32);

        const long DT_NULL = 0, DT_NEEDED = 1, DT_STRTAB = 5, DT_STRSZ = 10;
        int nEntries = (int)(oldDynFilesz / 16);
        var entries = new (long tag, ulong val)[nEntries];
        ulong strtabVaddr = 0;
        long strsz = 0;
        for (int i = 0; i < nEntries; i++)
        {
            long tag = (long)ReadU64(data, oldDynOffset + i * 16);
            ulong val = ReadU64(data, oldDynOffset + i * 16 + 8);
            entries[i] = (tag, val);
            if (tag == DT_STRTAB) strtabVaddr = val;
            if (tag == DT_STRSZ) strsz = (long)val;
        }
        if (strtabVaddr == 0) throw new InvalidDataException("No DT_STRTAB entry found.");

        long VaddrToOffset(ulong vaddr)
        {
            for (int i = 0; i < ePhnum; i++)
            {
                long off = ePhoff + i * ePhentsize;
                if (ReadU32(data, off) != PT_LOAD) continue;
                long pOffset = (long)ReadU64(data, off + 8);
                ulong pVaddr = ReadU64(data, off + 16);
                long pFilesz = (long)ReadU64(data, off + 32);
                if (vaddr >= pVaddr && vaddr < pVaddr + (ulong)pFilesz)
                    return pOffset + (long)(vaddr - pVaddr);
            }
            throw new InvalidDataException("Address not mapped in any PT_LOAD segment.");
        }

        long strtabOff = VaddrToOffset(strtabVaddr);
        byte[] oldStrtab = new byte[strsz];
        Array.Copy(data, strtabOff, oldStrtab, 0, strsz);

        byte[] newNameBytes = System.Text.Encoding.ASCII.GetBytes(newLibName + "\0");
        byte[] newStrtab = new byte[oldStrtab.Length + newNameBytes.Length];
        oldStrtab.CopyTo(newStrtab, 0);
        newNameBytes.CopyTo(newStrtab, oldStrtab.Length);
        long newNameStrOffset = oldStrtab.Length;

        int pad = (int)((8 - (newStrtab.Length % 8)) % 8);
        byte[] newStrtabPadded = new byte[newStrtab.Length + pad];
        newStrtab.CopyTo(newStrtabPadded, 0);

        long fileLen = data.Length;
        long newSegFileOff = (fileLen + Page - 1) & ~(long)(Page - 1);
        ulong newSegVaddr = (maxVaddrEnd + Page - 1) & ~(ulong)(Page - 1);

        ulong newDynstrVaddr = newSegVaddr;
        long newDynstrOff = newSegFileOff;
        ulong dynamicVaddr = newSegVaddr + (ulong)newStrtabPadded.Length;
        long dynamicOff = newSegFileOff + newStrtabPadded.Length;

        var newEntries = new System.Collections.Generic.List<(long tag, ulong val)>();
        bool inserted = false;
        foreach (var (tag, val) in entries)
        {
            if (tag == DT_STRTAB) newEntries.Add((DT_STRTAB, newDynstrVaddr));
            else if (tag == DT_STRSZ) newEntries.Add((DT_STRSZ, (ulong)newStrtab.Length));
            else if (tag == DT_NULL && !inserted)
            {
                newEntries.Add((DT_NEEDED, (ulong)newNameStrOffset));
                newEntries.Add((DT_NULL, 0));
                inserted = true;
            }
            else newEntries.Add((tag, val));
        }
        if (!inserted)
        {
            newEntries.Add((DT_NEEDED, (ulong)newNameStrOffset));
            newEntries.Add((DT_NULL, 0));
        }

        byte[] dynamicBytes = new byte[newEntries.Count * 16];
        for (int i = 0; i < newEntries.Count; i++)
        {
            WriteU64(dynamicBytes, i * 16, (ulong)newEntries[i].tag);
            WriteU64(dynamicBytes, i * 16 + 8, newEntries[i].val);
        }

        using MemoryStream ms = new();
        ms.Write(data, 0, data.Length);
        while (ms.Length < newSegFileOff) ms.WriteByte(0);
        ms.Write(newStrtabPadded, 0, newStrtabPadded.Length);
        ms.Write(dynamicBytes, 0, dynamicBytes.Length);

        int newPhnum = ePhnum + 1;
        long phdrStart = ms.Length;

        byte[] phdrsOut = new byte[newPhnum * ePhentsize];
        for (int i = 0; i < ePhnum; i++)
        {
            long src = ePhoff + i * ePhentsize;
            Array.Copy(data, src, phdrsOut, i * ePhentsize, ePhentsize);
            if (i == dynPhIndex)
            {
                long dst = i * ePhentsize;
                WriteU64(phdrsOut, dst + 8, (ulong)dynamicOff);
                WriteU64(phdrsOut, dst + 16, dynamicVaddr);
                WriteU64(phdrsOut, dst + 24, dynamicVaddr);
                WriteU64(phdrsOut, dst + 32, (ulong)dynamicBytes.Length);
                WriteU64(phdrsOut, dst + 40, (ulong)dynamicBytes.Length);
            }
        }
        long newLoadOff = ePhnum * ePhentsize;
        WriteU32(phdrsOut, newLoadOff, PT_LOAD);
        WriteU32(phdrsOut, newLoadOff + 4, 6); // RW
        WriteU64(phdrsOut, newLoadOff + 8, (ulong)newSegFileOff);
        WriteU64(phdrsOut, newLoadOff + 16, newSegVaddr);
        WriteU64(phdrsOut, newLoadOff + 24, newSegVaddr);
        WriteU64(phdrsOut, newLoadOff + 32, (ulong)newStrtabPadded.Length);
        WriteU64(phdrsOut, newLoadOff + 40, (ulong)newStrtabPadded.Length);
        WriteU64(phdrsOut, newLoadOff + 48, Page);

        ms.Write(phdrsOut, 0, phdrsOut.Length);

        byte[] outBytes = ms.ToArray();
        WriteU64(outBytes, 32, (ulong)phdrStart);
        WriteU16(outBytes, 56, (ushort)newPhnum);

        const uint SHT_DYNAMIC = 6, SHT_STRTAB = 3;
        for (int i = 0; i < eShnum; i++)
        {
            long off = eShoff + i * eShentsize;
            uint shType = ReadU32(data, off + 4);
            ulong shAddr = ReadU64(data, off + 16);
            if (shType == SHT_DYNAMIC && shAddr == oldDynVaddr)
            {
                WriteU64(outBytes, off + 16, dynamicVaddr);
                WriteU64(outBytes, off + 24, (ulong)dynamicOff);
                WriteU64(outBytes, off + 32, (ulong)dynamicBytes.Length);
            }
            if (shType == SHT_STRTAB && shAddr == strtabVaddr)
            {
                WriteU64(outBytes, off + 16, newDynstrVaddr);
                WriteU64(outBytes, off + 24, (ulong)newDynstrOff);
                WriteU64(outBytes, off + 32, (ulong)newStrtab.Length);
            }
        }

        logger.Log($"ELF patch: added '{newLibName}' as NEEDED, {data.Length} -> {outBytes.Length} bytes.");
        return outBytes;
    }
}
