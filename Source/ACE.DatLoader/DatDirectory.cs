using System.Collections.Generic;
using System.IO;

namespace ACE.DatLoader
{
    public class DatDirectory
    {
        private uint rootSectorOffset { get; }

        private uint blockSize { get; }


        private bool retail { get; }

        public DatDirectoryHeader DatDirectoryHeader { get; }

        public List<DatDirectory> Directories { get; } = new List<DatDirectory>();


        public DatDirectory(uint rootSectorOffset, uint blockSize, bool retail = false)
        {
            this.rootSectorOffset = rootSectorOffset;
            this.blockSize = blockSize;
            this.retail = retail;

            DatDirectoryHeader = new DatDirectoryHeader(retail);
        }

        public void Read(FileStream stream)
        {
            var headerReader = new DatReader(stream, rootSectorOffset, retail ? DatDirectoryHeader.RetailObjectSize : DatDirectoryHeader.ObjectSize, blockSize);

            using (var memoryStream = new MemoryStream(headerReader.Buffer))
            using (var reader = new BinaryReader(memoryStream))
                DatDirectoryHeader.Unpack(reader);

            // directory is allowed to have files + 1 subdirectories
            if (DatDirectoryHeader.Branches[0] != 0)
            {
                for (int i = 0; i < DatDirectoryHeader.EntryCount + 1; i++)
                {
                    var directory = new DatDirectory(DatDirectoryHeader.Branches[i], blockSize, retail);
                    directory.Read(stream);
                    Directories.Add(directory);
                }
            }
        }

        public void AddFilesToList(Dictionary<uint, DatFile> dicFiles)
        {
            Directories.ForEach(d => d.AddFilesToList(dicFiles));

            for (int i = 0; i < DatDirectoryHeader.EntryCount; i++)
                dicFiles[DatDirectoryHeader.Entries[i].ObjectId] = DatDirectoryHeader.Entries[i];
        }
    }
}
