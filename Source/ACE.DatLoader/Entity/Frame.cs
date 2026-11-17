using System.IO;
using System.Numerics;

namespace ACE.DatLoader.Entity
{
    /// <summary>
    /// Frame consists of a Vector3 Origin and a Quaternion Orientation
    /// </summary>
    public class Frame : IUnpackable
    {
        public Vector3 Origin { get; private set; }
        public Quaternion Orientation { get; private set; }

        public Frame()
        {
            Origin = Vector3.Zero;
            Orientation = Quaternion.Identity;
        }

        public Frame(ACE.Entity.Position position)
        {
            Init(position.Pos, position.Rotation);
        }

        public Frame(Vector3 origin, Quaternion orientation)
        {
            Init(origin, orientation);
        }

        public void Init(Vector3 origin, Quaternion orientation)
        {
            Origin = origin;
            Orientation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        }


        public void Unpack(BinaryReader reader)
        {
            Origin = reader.ReadVector3();

            var qw = reader.ReadSingle();
            var qx = reader.ReadSingle();
            var qy = reader.ReadSingle();
            var qz = reader.ReadSingle();
            Orientation = new Quaternion(qx, qy, qz, qw);
        }

        public void Pack(BinaryWriter writer)
        {
            writer.Write(Origin.X);
            writer.Write(Origin.Y);
            writer.Write(Origin.Z);
            writer.Write(Orientation.W);
            writer.Write(Orientation.X);
            writer.Write(Orientation.Y);
            writer.Write(Orientation.Z);
        }

        public override string ToString()
        {
            return Origin + " " + Orientation;
        }
    }
}
