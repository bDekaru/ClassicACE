using System;
using System.IO;

namespace ACE.DatLoader
{
    public enum DatSaveOutcome
    {
        Added,
        Replaced,
        RefusedOlderIteration,
    }

    public class DatAudit
    {
        public int Records { get; set; }
        public uint FreeBlocks { get; set; }
        public uint OwnedBlocks { get; set; }
        public uint OrphanedBlocks { get; set; }
    }

    /// <summary>
    /// Writes records into an existing post-ToD (client_*.dat) container, keeping the free list, the B-tree and the header the way the retail client keeps them:
    /// allocation is FIFO off the free-list head, freed chains go to the free-list tail, the file grows by appending pre-linked free blocks,
    /// the directory is an order-62 B-tree split top-down (30 / 1 / 30), and a replacement writes its new chain before the old one is freed.
    /// The transaction journal is only checked before and reset after a write, never replayed.
    /// </summary>
    public class DatWriter : IDisposable
    {
        private const long HeaderOffset = 0x140;
        private const long JournalOffset = 0x100;
        private const int HeaderBytes = 0x50;
        private const uint Magic = 0x5442;
        private const uint TransactionMagic = 0x4C50;

        private const int FileSizeField = 0x08;
        private const int FreeHeadField = 0x14;
        private const int FreeTailField = 0x18;
        private const int FreeCountField = 0x1C;
        private const int BTreeRootField = 0x20;

        private const uint FirstBlock = 0x400;
        private const uint FreeBit = 0x80000000;

        private const int FanOut = 62;
        private const int MaxEntries = 61;
        private const int SplitHalf = 30;
        private const int EntryBytes = 24;
        private const int CountOffset = FanOut * 4;
        private const int EntriesOffset = CountOffset + 4;
        private const int NodeBytes = EntriesOffset + MaxEntries * EntryBytes;

        /// <summary>Version for a record added without one. The EoR dats hold records of version 2 and 3 only (version 1 is just the iteration list), and 2 is the more common.</summary>
        public const ushort NewRecordVersion = 2;

        /// <summary>The record that holds the dat's set of iteration numbers (see <see cref="SaveIterationRecord"/>).</summary>
        public const uint IterationRecordId = 0xFFFF0001;

        /// <summary>Unix time stored in every record saved without a date, new or replaced (a build sets it to the date of the step it is running, so building the same recipe twice writes the same bytes).
        /// 0 keeps the stored date on a replacement and uses the current time for a new record.</summary>
        public uint RecordDate { get; set; }

        /// <summary>Gives the entry iteration of a record saved with a date (its own or <see cref="RecordDate"/>) when the caller passes none; a result of 0 leaves the iteration as it would be without it.</summary>
        public Func<uint, uint> IterationForDate { get; set; }

        private const uint RoomReserve = 0x33;
        private const uint ExpandBytes = 0x100000;

        private readonly FileStream stream;
        private readonly byte[] rawHeader = new byte[HeaderBytes];

        private uint blockSize;
        private uint fileSize;
        private uint freeHead;
        private uint freeTail;
        private uint freeCount;
        private uint btreeRoot;

        public string FilePath { get; }

        public DatWriter(string filePath)
        {
            FilePath = filePath;
            stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            try
            {
                ReadAt(HeaderOffset, rawHeader, rawHeader.Length);

                if (BitConverter.ToUInt32(rawHeader, 0) != Magic)
                    throw new InvalidDataException($"{filePath} is not a post-ToD dat (no header magic at 0x{HeaderOffset:X}).");

                blockSize = BitConverter.ToUInt32(rawHeader, 4);
                fileSize = BitConverter.ToUInt32(rawHeader, FileSizeField);
                freeHead = BitConverter.ToUInt32(rawHeader, FreeHeadField);
                freeTail = BitConverter.ToUInt32(rawHeader, FreeTailField);
                freeCount = BitConverter.ToUInt32(rawHeader, FreeCountField);
                btreeRoot = BitConverter.ToUInt32(rawHeader, BTreeRootField);

                if (blockSize < 0x100 || fileSize != stream.Length)
                    throw new InvalidDataException($"{filePath}: header block size {blockSize} / file size {fileSize} do not match the file ({stream.Length} bytes).");

                RequireQuiescent();
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates an empty post-ToD dat at <paramref name="path"/>: the template's first 0x400 bytes (header, journal, pack versions) and a pre-linked free list with an empty directory root.
        /// The template is only read; the new file keeps its block size and pack versions, so build it from a dat of the same kind.
        /// </summary>
        public static void CreateEmpty(string path, string templateDat)
        {
            const uint initialBlocks = 64;

            if (File.Exists(path))
                throw new ArgumentException($"{path} already exists.");

            var head = new byte[FirstBlock];

            using (var template = new FileStream(templateDat, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (template.Length < FirstBlock)
                    throw new InvalidDataException($"{templateDat} is too short to be a dat.");

                template.Read(head, 0, head.Length);
            }

            if (BitConverter.ToUInt32(head, (int)HeaderOffset) != Magic)
                throw new InvalidDataException($"{templateDat} is not a post-ToD dat (no header magic at 0x{HeaderOffset:X}).");

            var size = BitConverter.ToUInt32(head, (int)HeaderOffset + 4);

            if (size < 0x100)
                throw new InvalidDataException($"{templateDat}: implausible block size {size}.");

            var last = FirstBlock + (initialBlocks - 1) * size;

            Array.Copy(BitConverter.GetBytes(FirstBlock + initialBlocks * size), 0, head, HeaderOffset + FileSizeField, 4);
            Array.Copy(BitConverter.GetBytes(FirstBlock), 0, head, HeaderOffset + FreeHeadField, 4);
            Array.Copy(BitConverter.GetBytes(last), 0, head, HeaderOffset + FreeTailField, 4);
            Array.Copy(BitConverter.GetBytes(initialBlocks), 0, head, HeaderOffset + FreeCountField, 4);
            Array.Copy(BitConverter.GetBytes(0u), 0, head, HeaderOffset + BTreeRootField, 4);

            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                file.Write(head, 0, head.Length);

                var block = new byte[size];

                for (uint i = 0; i < initialBlocks; i++)
                {
                    var next = i == initialBlocks - 1 ? 0 : FirstBlock + (i + 1) * size;
                    Array.Copy(BitConverter.GetBytes(next | FreeBit), 0, block, 0, 4);
                    file.Write(block, 0, block.Length);
                }
            }

            using (var writer = new DatWriter(path))
            {
                writer.btreeRoot = writer.AllocateEmptyNode();
                writer.SaveFileInfo();
                writer.ClearTransaction();
            }
        }

        public void Dispose()
        {
            stream.Flush();
            stream.Dispose();
        }

        private uint PayloadPerBlock => blockSize - 4;

        // ---------------------------------------------------------------------------------------
        // Raw access
        // ---------------------------------------------------------------------------------------

        private void ReadAt(long offset, byte[] buffer, int count)
        {
            stream.Seek(offset, SeekOrigin.Begin);

            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                    throw new EndOfStreamException($"{FilePath}: read past the end at 0x{offset:X}.");
                read += n;
            }
        }

        private void WriteAt(long offset, byte[] buffer, int count)
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Write(buffer, 0, count);
        }

        private uint LinkAt(uint block)
        {
            CheckBlock(block);

            var buffer = new byte[4];
            ReadAt(block, buffer, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        private void WriteLink(uint block, uint link)
        {
            WriteAt(block, BitConverter.GetBytes(link), 4);
        }

        private void CheckBlock(uint block)
        {
            if (block < FirstBlock || block >= fileSize || block % blockSize != 0)
                throw new InvalidDataException($"{FilePath}: block 0x{block:X} is not a valid block offset.");
        }

        private void SaveFileInfo()
        {
            Array.Copy(BitConverter.GetBytes(fileSize), 0, rawHeader, FileSizeField, 4);
            Array.Copy(BitConverter.GetBytes(freeHead), 0, rawHeader, FreeHeadField, 4);
            Array.Copy(BitConverter.GetBytes(freeTail), 0, rawHeader, FreeTailField, 4);
            Array.Copy(BitConverter.GetBytes(freeCount), 0, rawHeader, FreeCountField, 4);
            Array.Copy(BitConverter.GetBytes(btreeRoot), 0, rawHeader, BTreeRootField, 4);

            WriteAt(HeaderOffset, rawHeader, rawHeader.Length);
        }

        private void RequireQuiescent()
        {
            var record = new byte[5];
            ReadAt(JournalOffset, record, record.Length);

            var magic = BitConverter.ToUInt32(record, 1);
            if (magic != TransactionMagic)
                throw new InvalidDataException($"{FilePath}: the transaction journal has the magic 0x{magic:X}, not 0x{TransactionMagic:X}.");

            if (record[0] != 0)
                throw new InvalidDataException($"{FilePath}: a transaction (type {record[0]}) is pending in the journal.");
        }

        private void ClearTransaction()
        {
            var record = new byte[5];
            Array.Copy(BitConverter.GetBytes(TransactionMagic), 0, record, 1, 4);
            WriteAt(JournalOffset, record, record.Length);
        }

        // ---------------------------------------------------------------------------------------
        // Block chains
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Stores the payload in the first blocks of the free list, starting at its head. The header goes to disk before the chain's last block.
        /// </summary>
        private void StoreData(byte[] payload, uint head)
        {
            var per = (int)PayloadPerBlock;
            var remaining = payload.Length;
            var source = 0;
            var dest = head;
            uint taken = 1;
            var next = LinkAt(dest) & ~FreeBit;
            var block = new byte[blockSize];

            while (remaining > 0)
            {
                uint link;
                int chunk;

                if (per < remaining)
                {
                    if (next == 0)
                        throw new InvalidDataException($"{FilePath}: the free list ran out.");

                    var after = LinkAt(next) & ~FreeBit;
                    taken++;
                    link = next;
                    next = after;
                    chunk = per;
                }
                else
                {
                    if (taken > freeCount)
                        throw new InvalidDataException($"{FilePath}: the free list ran out.");

                    freeCount -= taken;
                    freeHead = next;
                    SaveFileInfo();
                    link = 0;
                    chunk = remaining;
                }

                Array.Copy(BitConverter.GetBytes(link), 0, block, 0, 4);
                Array.Copy(payload, source, block, 4, chunk);
                WriteAt(dest, block, chunk + 4);

                remaining -= chunk;
                source += chunk;
                dest = link;
            }
        }

        private byte[] LoadData(uint id, uint offset, int size)
        {
            var per = (int)PayloadPerBlock;
            var result = new byte[size];
            var remaining = size;
            var position = 0;
            var block = new byte[blockSize];

            while (remaining > 0 && offset != 0)
            {
                CheckBlock(offset);

                var take = Math.Min(remaining, per);
                ReadAt(offset, block, take + 4);

                var link = BitConverter.ToUInt32(block, 0);
                if ((link & FreeBit) != 0)
                    throw new InvalidDataException($"{FilePath}: record {id:X8} runs into a free block at 0x{offset:X}.");

                Array.Copy(block, 4, result, position, take);
                position += take;
                remaining -= take;
                offset = link;
            }

            if (remaining != 0)
                throw new InvalidDataException($"{FilePath}: the chain of record {id:X8} is {remaining} bytes short.");

            return result;
        }

        private void DeleteBlocks(uint head)
        {
            if (head == 0)
                return;

            uint freed = 0;
            var current = head;
            uint tail;

            while (true)
            {
                var link = LinkAt(current);
                if ((link & FreeBit) != 0)
                    throw new InvalidDataException($"{FilePath}: block 0x{current:X} is freed twice.");

                WriteLink(current, link | FreeBit);
                freed++;

                var next = link & ~FreeBit;
                if (next == 0)
                {
                    tail = current;
                    break;
                }

                current = next;
            }

            WriteLink(freeTail, head | FreeBit);
            freeTail = tail;
            freeCount += freed;
            SaveFileInfo();
        }

        private void ExpandFile(uint bytes)
        {
            var count = bytes / blockSize;
            if (count == 0 || freeTail == 0)
                throw new InvalidDataException($"{FilePath}: cannot grow the file.");

            freeCount += count;

            var current = fileSize;
            var last = current;
            var block = new byte[blockSize];

            for (uint i = 0; i < count; i++)
            {
                last = current;
                current = checked(last + blockSize);

                Array.Copy(BitConverter.GetBytes(current | FreeBit), 0, block, 0, 4);
                WriteAt(last, block, block.Length);
            }

            var oldTail = freeTail;
            var firstNew = fileSize;

            WriteLink(last, FreeBit);
            fileSize = current;
            freeTail = last;
            WriteLink(oldTail, firstNew | FreeBit);
            SaveFileInfo();
        }

        private void CheckRoom(int size)
        {
            var need = (uint)(size / (int)PayloadPerBlock) + RoomReserve;

            while (freeCount <= need)
                ExpandFile(ExpandBytes);
        }

        // ---------------------------------------------------------------------------------------
        // The directory
        // ---------------------------------------------------------------------------------------

        private struct Entry
        {
            public uint Bits;
            public uint Id;
            public uint Offset;
            public uint Size;
            public uint Date;
            public uint Iteration;
        }

        private class Node
        {
            public readonly byte[] Bytes = new byte[NodeBytes];

            public uint Child(int i) => BitConverter.ToUInt32(Bytes, i * 4);

            public void SetChild(int i, uint value) => Array.Copy(BitConverter.GetBytes(value), 0, Bytes, i * 4, 4);

            public bool IsLeaf => Child(0) == 0;

            public int Count => (int)BitConverter.ToUInt32(Bytes, CountOffset);

            public void SetCount(int n) => Array.Copy(BitConverter.GetBytes((uint)n), 0, Bytes, CountOffset, 4);

            public Entry GetEntry(int i)
            {
                var at = EntriesOffset + i * EntryBytes;

                return new Entry
                {
                    Bits = BitConverter.ToUInt32(Bytes, at),
                    Id = BitConverter.ToUInt32(Bytes, at + 4),
                    Offset = BitConverter.ToUInt32(Bytes, at + 8),
                    Size = BitConverter.ToUInt32(Bytes, at + 12),
                    Date = BitConverter.ToUInt32(Bytes, at + 16),
                    Iteration = BitConverter.ToUInt32(Bytes, at + 20),
                };
            }

            public void SetEntry(int i, Entry entry)
            {
                var at = EntriesOffset + i * EntryBytes;

                Array.Copy(BitConverter.GetBytes(entry.Bits), 0, Bytes, at, 4);
                Array.Copy(BitConverter.GetBytes(entry.Id), 0, Bytes, at + 4, 4);
                Array.Copy(BitConverter.GetBytes(entry.Offset), 0, Bytes, at + 8, 4);
                Array.Copy(BitConverter.GetBytes(entry.Size), 0, Bytes, at + 12, 4);
                Array.Copy(BitConverter.GetBytes(entry.Date), 0, Bytes, at + 16, 4);
                Array.Copy(BitConverter.GetBytes(entry.Iteration), 0, Bytes, at + 20, 4);
            }

            public void MoveEntry(int to, int from) => Array.Copy(Bytes, EntriesOffset + from * EntryBytes, Bytes, EntriesOffset + to * EntryBytes, EntryBytes);
        }

        private Node ReadNode(uint offset)
        {
            var node = new Node();
            Array.Copy(LoadData(0, offset, NodeBytes), node.Bytes, NodeBytes);

            if (node.Count >= FanOut)
                throw new InvalidDataException($"{FilePath}: the node at 0x{offset:X} claims {node.Count} entries.");

            return node;
        }

        /// <summary>
        /// A node's chain was allocated for exactly its size, so a write only rewrites that chain's payload and leaves the links.
        /// </summary>
        private void WriteNode(uint offset, Node node)
        {
            var per = (int)PayloadPerBlock;
            var want = (NodeBytes + per - 1) / per;
            var dest = offset;
            var source = 0;
            var used = 0;
            var block = new byte[blockSize];

            while (source < NodeBytes)
            {
                used++;
                if (used > want)
                    throw new InvalidDataException($"{FilePath}: the node at 0x{offset:X} has a chain longer than {want} blocks.");

                var chunk = Math.Min(NodeBytes - source, per);
                var last = source + chunk >= NodeBytes;
                var link = last ? 0 : LinkAt(dest);

                if ((link & FreeBit) != 0 || (link == 0 && !last))
                    throw new InvalidDataException($"{FilePath}: the node at 0x{offset:X} has a broken chain.");

                Array.Copy(BitConverter.GetBytes(link), 0, block, 0, 4);
                Array.Copy(node.Bytes, source, block, 4, chunk);
                WriteAt(dest, block, chunk + 4);

                source += chunk;
                dest = link;
            }

            if (used != want)
                throw new InvalidDataException($"{FilePath}: the node at 0x{offset:X} has a chain shorter than {want} blocks.");
        }

        private uint AllocateEmptyNode()
        {
            var offset = freeHead;
            CheckBlock(offset);
            StoreData(new byte[NodeBytes], offset);
            return offset;
        }

        private bool Find(uint id, out uint nodeOffset, out int index, out Entry entry)
        {
            nodeOffset = 0;
            index = 0;
            entry = default(Entry);

            if (id == 0)
                return false;

            var offset = btreeRoot;

            while (true)
            {
                var node = ReadNode(offset);
                var lo = 0;
                var hi = node.Count;

                while (lo < hi)
                {
                    var mid = (lo + hi) / 2;
                    var key = node.GetEntry(mid).Id;

                    if (key == id)
                    {
                        nodeOffset = offset;
                        index = mid;
                        entry = node.GetEntry(mid);
                        return true;
                    }

                    if (key < id)
                        lo = mid + 1;
                    else
                        hi = mid;
                }

                if (node.IsLeaf)
                    return false;

                offset = node.Child(lo);
            }
        }

        private void SplitChild(uint parentOffset, int index)
        {
            var parent = ReadNode(parentOffset);
            var parentCount = parent.Count;
            var fullOffset = parent.Child(index);
            var full = ReadNode(fullOffset);

            if (full.Count != MaxEntries)
                throw new InvalidDataException($"{FilePath}: asked to split the node at 0x{fullOffset:X} holding {full.Count} entries.");

            var rightOffset = AllocateEmptyNode();
            var right = new Node();

            for (var k = 0; k < SplitHalf; k++)
                right.SetEntry(k, full.GetEntry(SplitHalf + 1 + k));

            if (!full.IsLeaf)
                for (var k = 0; k <= SplitHalf; k++)
                    right.SetChild(k, full.Child(SplitHalf + 1 + k));

            right.SetCount(SplitHalf);
            WriteNode(rightOffset, right);

            var promoted = full.GetEntry(SplitHalf);
            for (var k = parentCount; k >= index + 1; k--)
            {
                parent.SetChild(k + 1, parent.Child(k));
                parent.MoveEntry(k, k - 1);
            }

            parent.SetChild(index + 1, rightOffset);
            parent.SetEntry(index, promoted);
            parent.SetCount(parentCount + 1);
            WriteNode(parentOffset, parent);

            full.SetCount(SplitHalf);
            WriteNode(fullOffset, full);
        }

        private void DescendToAdd(Entry entry, byte[] payload)
        {
            var id = entry.Id;

            if (ReadNode(btreeRoot).Count == MaxEntries)
            {
                var oldRoot = btreeRoot;
                var newRoot = AllocateEmptyNode();
                var node = new Node();
                node.SetChild(0, oldRoot);
                node.SetCount(0);

                btreeRoot = newRoot;
                SaveFileInfo();
                WriteNode(newRoot, node);
                SplitChild(newRoot, 0);
            }

            var offset = btreeRoot;

            while (true)
            {
                var node = ReadNode(offset);
                var n = node.Count;

                if (node.IsLeaf)
                {
                    InsertIntoLeaf(offset, node, n, entry, payload);
                    return;
                }

                var i = n;
                while (i > 0 && node.GetEntry(i - 1).Id > id)
                    i--;

                var childOffset = node.Child(i);

                if (ReadNode(childOffset).Count == MaxEntries)
                {
                    SplitChild(offset, i);

                    var parent = ReadNode(offset);
                    if (parent.GetEntry(i).Id < id)
                        i++;

                    offset = parent.Child(i);
                }
                else
                    offset = childOffset;
            }
        }

        private void InsertIntoLeaf(uint offset, Node node, int count, Entry entry, byte[] payload)
        {
            var i = count;
            while (i > 0 && node.GetEntry(i - 1).Id > entry.Id)
            {
                node.MoveEntry(i, i - 1);
                i--;
            }

            var head = freeHead;
            entry.Offset = head;
            entry.Size = (uint)payload.Length;
            node.SetEntry(i, entry);
            node.SetCount(count + 1);

            StoreData(payload, head);
            WriteNode(offset, node);
        }

        private void ReplaceEntry(uint nodeOffset, int index, Entry entry, byte[] payload, uint oldOffset)
        {
            var head = freeHead;
            var node = ReadNode(nodeOffset);
            entry.Offset = head;
            entry.Size = (uint)payload.Length;
            node.SetEntry(index, entry);

            StoreData(payload, head);
            WriteNode(nodeOffset, node);
            DeleteBlocks(oldOffset);
        }

        // ---------------------------------------------------------------------------------------
        // Audit
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Walks every directory node, record chain and the free list. Throws when a block is owned twice, a chain has the wrong length or end,
        /// the keys are not strictly ascending, or the free list does not match the header. Blocks reachable from nowhere are counted, not rejected.
        /// </summary>
        public DatAudit Audit()
        {
            var per = PayloadPerBlock;
            var owner = new System.Collections.Generic.Dictionary<uint, string>();
            var keys = new System.Collections.Generic.List<uint>();

            void Claim(uint head, long size, string who)
            {
                var blocks = (uint)((size + per - 1) / per);
                var block = head;

                for (uint i = 0; i < blocks; i++)
                {
                    CheckBlock(block);

                    if (owner.TryGetValue(block, out var other))
                        throw new InvalidDataException($"{FilePath}: block 0x{block:X} is in both {other} and {who}.");

                    owner[block] = who;

                    var link = LinkAt(block);
                    var last = i == blocks - 1;

                    if ((link & FreeBit) != 0 || (last && link != 0) || (!last && link == 0))
                        throw new InvalidDataException($"{FilePath}: the chain of {who} has a bad link 0x{link:X} at block 0x{block:X}.");

                    block = link;
                }
            }

            void Walk(uint offset, int depth)
            {
                if (depth > 64)
                    throw new InvalidDataException($"{FilePath}: the directory is deeper than 64 levels.");

                Claim(offset, NodeBytes, $"node 0x{offset:X}");

                var node = ReadNode(offset);
                var count = node.Count;

                for (var i = 0; i < count; i++)
                {
                    if (!node.IsLeaf)
                        Walk(node.Child(i), depth + 1);

                    var entry = node.GetEntry(i);
                    keys.Add(entry.Id);
                    Claim(entry.Offset, entry.Size, $"record {entry.Id:X8}");
                }

                if (!node.IsLeaf)
                    Walk(node.Child(count), depth + 1);
            }

            if (fileSize != stream.Length)
                throw new InvalidDataException($"{FilePath}: header file size {fileSize} is not the real size {stream.Length}.");

            Walk(btreeRoot, 0);

            for (var i = 1; i < keys.Count; i++)
                if (keys[i - 1] >= keys[i])
                    throw new InvalidDataException($"{FilePath}: the directory keys are not strictly ascending at {keys[i]:X8}.");

            var free = freeHead;
            for (uint i = 0; i < freeCount; i++)
            {
                CheckBlock(free);

                var link = LinkAt(free);
                if ((link & FreeBit) == 0)
                    throw new InvalidDataException($"{FilePath}: block 0x{free:X} on the free list is not flagged free.");

                if (owner.TryGetValue(free, out var other))
                    throw new InvalidDataException($"{FilePath}: free block 0x{free:X} is also in {other}.");

                owner[free] = "free list";

                if (i == freeCount - 1)
                {
                    if (free != freeTail || link != FreeBit)
                        throw new InvalidDataException($"{FilePath}: the free list does not end at the free tail 0x{freeTail:X}.");
                }
                else
                    free = link & ~FreeBit;
            }

            var total = (fileSize - FirstBlock) / blockSize;

            return new DatAudit
            {
                Records = keys.Count,
                FreeBlocks = freeCount,
                OwnedBlocks = (uint)owner.Count,
                OrphanedBlocks = total - (uint)owner.Count,
            };
        }

        // ---------------------------------------------------------------------------------------
        // Public operations
        // ---------------------------------------------------------------------------------------

        public bool Contains(uint id)
        {
            return Find(id, out _, out _, out _);
        }

        /// <summary>The version, iteration and date stored in the record's directory entry, or false when the id is not in the dat.</summary>
        public bool TryGetEntryInfo(uint id, out ushort version, out uint iteration, out uint date)
        {
            var found = Find(id, out _, out _, out var entry);

            version = (ushort)(entry.Bits >> 16);
            iteration = entry.Iteration;
            date = entry.Date;
            return found;
        }

        /// <summary>
        /// The record's bytes exactly as stored, or null when the id is not in the dat.
        /// </summary>
        public byte[] Read(uint id)
        {
            if (!Find(id, out _, out _, out var entry))
                return null;

            return LoadData(id, entry.Offset, (int)entry.Size);
        }

        /// <summary>
        /// Adds the record, or replaces it when the id is already there.
        /// </summary>
        /// <param name="version">The entry's version (its flags word is version &lt;&lt; 16), which the client uses to choose how the payload is unpacked. Null keeps the stored version on a replacement and uses <see cref="NewRecordVersion"/> for a new record.</param>
        /// <param name="iteration">The entry's iteration. 0 keeps the stored iteration on a replacement (a new record gets 0); a non-zero value older than the stored one refuses the write.</param>
        /// <param name="date">Unix time stored in the entry. 0 uses <see cref="RecordDate"/>; when that is 0 too, a replacement keeps the stored date and a new record gets the current time.</param>
        public DatSaveOutcome Save(uint id, byte[] payload, uint iteration = 0, ushort? version = null, uint date = 0)
        {
            if (id == 0)
                throw new ArgumentException("Record id 0 is not valid.", nameof(id));

            if (payload == null || payload.Length == 0)
                throw new ArgumentException($"Record {id:X8} has no data.", nameof(payload));

            if (version == 0)
                throw new ArgumentException($"Record {id:X8} cannot have version 0.", nameof(version));

            CheckRoom(payload.Length);

            var found = Find(id, out var nodeOffset, out var index, out var old);

            if (date == 0)
                date = RecordDate;

            if (date != 0 && iteration == 0 && IterationForDate != null)
                iteration = IterationForDate(date);

            if (date == 0)
                date = found ? old.Date : (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var entry = new Entry
            {
                Bits = (uint)(version ?? (found ? (ushort)(old.Bits >> 16) : NewRecordVersion)) << 16,
                Id = id,
                Size = (uint)payload.Length,
                Date = date,
                Iteration = iteration != 0 || !found ? iteration : old.Iteration,
            };

            DatSaveOutcome outcome;

            if (!found)
            {
                DescendToAdd(entry, payload);
                outcome = DatSaveOutcome.Added;
            }
            else
            {
                if (iteration != 0 && iteration < old.Iteration)
                    return DatSaveOutcome.RefusedOlderIteration;

                ReplaceEntry(nodeOffset, index, entry, payload, old.Offset);
                outcome = DatSaveOutcome.Replaced;
            }

            SaveFileInfo();
            ClearTransaction();
            return outcome;
        }

        /// <summary>
        /// Writes the iteration record the retail dats carry: the dat holds iterations 1 to <paramref name="total"/> as one run (total, minus total, first number 1), version 1, entry iteration 1.
        /// </summary>
        public void SaveIterationRecord(uint total, uint date = 0)
        {
            var payload = new byte[12];

            BitConverter.GetBytes(total).CopyTo(payload, 0);
            BitConverter.GetBytes(-(int)total).CopyTo(payload, 4);
            BitConverter.GetBytes(1).CopyTo(payload, 8);

            Save(IterationRecordId, payload, 1, 1, date);
        }
    }
}
