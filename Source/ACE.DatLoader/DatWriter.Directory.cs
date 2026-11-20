using System;
using System.IO;

namespace ACE.DatLoader
{
    /// <summary>The directory of the container: an order-62 B-tree of entries, split top-down.</summary>
    public partial class DatWriter
    {
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
    }
}
