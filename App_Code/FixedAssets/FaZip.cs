using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// =====================================================================
//  Fixed Assets: minimal ZIP reader and writer for .xlsx files, so the
//  import needs nothing beyond System.dll (no WindowsBase reference in
//  web.config). Writer stores entries uncompressed; reader handles
//  stored and deflated entries via the central directory.
// =====================================================================
public static class FaZip
{
    private static uint[] _crc;

    private static uint Crc32(byte[] data)
    {
        if (_crc == null)
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            _crc = t;
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data) crc = _crc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    public static byte[] Write(List<KeyValuePair<string, byte[]>> files)
    {
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            var central = new MemoryStream();
            var cw = new BinaryWriter(central);
            foreach (var f in files)
            {
                byte[] name = Encoding.UTF8.GetBytes(f.Key);
                uint crc = Crc32(f.Value);
                uint offset = (uint)ms.Position;
                w.Write(0x04034b50u); w.Write((ushort)20); w.Write((ushort)0x0800); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0x21);
                w.Write(crc); w.Write((uint)f.Value.Length); w.Write((uint)f.Value.Length); w.Write((ushort)name.Length); w.Write((ushort)0);
                w.Write(name); w.Write(f.Value);
                cw.Write(0x02014b50u); cw.Write((ushort)20); cw.Write((ushort)20); cw.Write((ushort)0x0800); cw.Write((ushort)0); cw.Write((ushort)0); cw.Write((ushort)0x21);
                cw.Write(crc); cw.Write((uint)f.Value.Length); cw.Write((uint)f.Value.Length); cw.Write((ushort)name.Length);
                cw.Write((ushort)0); cw.Write((ushort)0); cw.Write((ushort)0); cw.Write((ushort)0); cw.Write(0u); cw.Write(offset); cw.Write(name);
            }
            cw.Flush();
            uint cdOffset = (uint)ms.Position;
            byte[] cd = central.ToArray();
            w.Write(cd);
            w.Write(0x06054b50u); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)files.Count); w.Write((ushort)files.Count);
            w.Write((uint)cd.Length); w.Write(cdOffset); w.Write((ushort)0);
            w.Flush();
            return ms.ToArray();
        }
    }

    /// <summary>Every entry of a ZIP file, by name.</summary>
    public static Dictionary<string, byte[]> Read(byte[] zip)
    {
        var outp = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        int eocd = -1;
        for (int i = zip.Length - 22; i >= Math.Max(0, zip.Length - 65557); i--)
            if (BitConverter.ToUInt32(zip, i) == 0x06054b50u) { eocd = i; break; }
        if (eocd < 0) throw new InvalidDataException("Not a ZIP file.");
        int count = BitConverter.ToUInt16(zip, eocd + 10);
        int p = (int)BitConverter.ToUInt32(zip, eocd + 16);
        for (int n = 0; n < count; n++)
        {
            if (BitConverter.ToUInt32(zip, p) != 0x02014b50u) throw new InvalidDataException("Bad ZIP directory.");
            int method = BitConverter.ToUInt16(zip, p + 10);
            int csize = (int)BitConverter.ToUInt32(zip, p + 20);
            int usize = (int)BitConverter.ToUInt32(zip, p + 24);
            int nlen = BitConverter.ToUInt16(zip, p + 28), xlen = BitConverter.ToUInt16(zip, p + 30), clen = BitConverter.ToUInt16(zip, p + 32);
            int local = (int)BitConverter.ToUInt32(zip, p + 42);
            string name = Encoding.UTF8.GetString(zip, p + 46, nlen);
            p += 46 + nlen + xlen + clen;
            int lnlen = BitConverter.ToUInt16(zip, local + 26), lxlen = BitConverter.ToUInt16(zip, local + 28);
            int data = local + 30 + lnlen + lxlen;
            byte[] raw = new byte[csize];
            Buffer.BlockCopy(zip, data, raw, 0, csize);
            if (method == 0) outp[name] = raw;
            else if (method == 8)
            {
                using (var src = new MemoryStream(raw))
                using (var ds = new DeflateStream(src, CompressionMode.Decompress))
                using (var dst = new MemoryStream(usize > 0 ? usize : 4096))
                {
                    ds.CopyTo(dst);
                    outp[name] = dst.ToArray();
                }
            }
        }
        return outp;
    }
}
