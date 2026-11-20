using System.IO;

namespace ACE.DatLoader
{
    public interface IPackable
    {
        void Pack(BinaryWriter writer);
    }
}
