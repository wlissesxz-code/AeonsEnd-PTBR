using System;
using System.IO;
using System.IO.Compression;
using System.Text;

// AEPATCH1: gzip stream containing an 8-byte signature, target length,
// copy (1, Int64 source offset, Int32 length), literal (2, Int32 length, bytes),
// and end (0) records. Hashes of the base, patch, and result are checked by the installer.
public static class AeonDelta
{
    public static void Apply(string original, string patch, string output, long expectedLength)
    {
        if (String.Equals(Path.GetFullPath(original), Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Input and output paths must differ.");
        using (var source = File.OpenRead(original))
        using (var packed = File.OpenRead(patch))
        using (var gzip = new GZipStream(packed, CompressionMode.Decompress))
        using (var reader = new BinaryReader(gzip, Encoding.UTF8))
        using (var target = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "AEPATCH1")
                throw new InvalidDataException("Invalid patch signature.");
            long length = reader.ReadInt64();
            if (length != expectedLength || length < 0) throw new InvalidDataException("Invalid target length.");
            byte[] buffer = new byte[65536];
            long written = 0;
            while (true)
            {
                byte operation = reader.ReadByte();
                if (operation == 0) break;
                long offset = operation == 1 ? reader.ReadInt64() : 0;
                int count = reader.ReadInt32();
                if ((operation != 1 && operation != 2) || count <= 0 || count > length - written)
                    throw new InvalidDataException("Invalid patch record.");
                if (operation == 1)
                {
                    if (offset < 0 || offset > source.Length || count > source.Length - offset)
                        throw new InvalidDataException("Copy record exceeds original file.");
                    source.Position = offset;
                }
                int remaining = count;
                while (remaining > 0)
                {
                    int n = operation == 1 ? source.Read(buffer, 0, Math.Min(buffer.Length, remaining))
                                           : reader.Read(buffer, 0, Math.Min(buffer.Length, remaining));
                    if (n == 0) throw new EndOfStreamException("Incomplete patch or original file.");
                    target.Write(buffer, 0, n);
                    remaining -= n;
                }
                written += count;
            }
            if (written != length || gzip.ReadByte() != -1)
                throw new InvalidDataException("Patch did not produce the expected length.");
        }
    }
}

