using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ACE.ClientPatcher
{
    public enum EditState
    {
        Applied,
        Original,
        Other
    }

    /// <summary>The client executable in memory, patched in place by virtual address.</summary>
    public sealed class ClientExe
    {
        /// <summary>The End of Retail client (11.6096, 2015-06-12), the only input the patches are written for.</summary>
        public const string RetailMd5 = "116d9a66a70b6af449dc3a28d82f2f6d";
        public const string RetailSha256 = "bca95bbebed4b9ed1ff09d0da83144e2fc4208f63ad7ada5cb47c3ca207ccba9";

        private const uint ImageBase = 0x400000;

        private readonly byte[] data;
        private readonly List<(uint Va, uint Size, uint RawOffset)> sections = new();

        public ClientExe(byte[] data)
        {
            this.data = data;

            var pe = BitConverter.ToInt32(data, 0x3C);
            var sectionCount = BitConverter.ToUInt16(data, pe + 6);
            var optionalHeaderSize = BitConverter.ToUInt16(data, pe + 20);

            for (var i = 0; i < sectionCount; i++)
            {
                var header = pe + 24 + optionalHeaderSize + i * 40;
                sections.Add((ImageBase + BitConverter.ToUInt32(data, header + 12), BitConverter.ToUInt32(data, header + 16), BitConverter.ToUInt32(data, header + 20)));
            }
        }

        /// <summary>Reads any client executable, patched or not (for check).</summary>
        public static ClientExe Load(string path) => new ClientExe(File.ReadAllBytes(path));

        /// <summary>Reads the file and refuses anything but the retail client.</summary>
        public static ClientExe LoadRetail(string path)
        {
            var exe = Load(path);

            if (Convert.ToHexString(MD5.HashData(exe.data)).ToLowerInvariant() != RetailMd5 || Convert.ToHexString(SHA256.HashData(exe.data)).ToLowerInvariant() != RetailSha256)
                throw new InvalidDataException($"{path} is not the End of Retail acclient.exe (11.6096, MD5 {RetailMd5}).");

            return exe;
        }

        /// <summary>Replaces the bytes of the edit, after checking that the client has the bytes it expects there.</summary>
        public void Apply(PatchEdit edit)
        {
            var was = Convert.FromHexString(edit.Was);
            var now = Convert.FromHexString(edit.Now);

            var where = edit.Address ?? "file offset " + edit.FileOffset;

            if (was.Length != now.Length)
                throw new InvalidDataException($"{where}: the edit changes the length ({was.Length} -> {now.Length} bytes).");

            var offset = Locate(edit);

            if (!data.AsSpan(offset, was.Length).SequenceEqual(was))
                throw new InvalidDataException($"{where}: the client does not have the expected bytes {edit.Was} ({edit.Note}).");

            now.CopyTo(data, offset);
        }

        /// <summary>Whether the client has the bytes the edit writes, the bytes it replaces, or something else (another edit of the catalogue rewrote them, or a different client).</summary>
        public EditState Inspect(PatchEdit edit)
        {
            var offset = Locate(edit);

            if (offset < 0 || offset + edit.Length > data.Length)
                return EditState.Other;

            var current = data.AsSpan(offset, edit.Length);

            if (current.SequenceEqual(Convert.FromHexString(edit.Now)))
                return EditState.Applied;

            return current.SequenceEqual(Convert.FromHexString(edit.Was)) ? EditState.Original : EditState.Other;
        }

        private int Locate(PatchEdit edit) => edit.FileOffset != null ? edit.Offset : FileOffset(edit.Va);

        public void Save(string path, bool force)
        {
            if (File.Exists(path) && !force)
                throw new IOException($"{path} exists; use --force to replace it.");

            File.WriteAllBytes(path, data);
        }

        private int FileOffset(uint va)
        {
            foreach (var section in sections)
            {
                if (va >= section.Va && va < section.Va + section.Size)
                    return (int)(section.RawOffset + va - section.Va);
            }

            throw new InvalidDataException($"0x{va:X} is not inside a section of the client.");
        }
    }
}
