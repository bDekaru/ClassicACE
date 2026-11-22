using System.IO;
using System.Numerics;

namespace ACE.DatLoader.Entity
{
    public class Sphere : IUnpackable, IPackable
    {
        public Vector3 Origin { get; private set; }
        public float Radius { get; private set; }

        /// <summary>Scales the sphere about a point.</summary>
        public void Scale(Vector3 center, float factor)
        {
            Origin = center + (Origin - center) * factor;
            Radius *= factor;
        }

        public void Unpack(BinaryReader reader)
        {
            Origin = reader.ReadVector3();
            Radius = reader.ReadSingle();
        }

        public void Pack(BinaryWriter writer)
        {
            writer.WriteVector3(Origin);
            writer.Write(Radius);
        }

        public static Sphere CreateDummySphere()
        {
            var sphere = new Sphere();
            sphere.Origin = Vector3.Zero;
            return sphere;
        }
    }
}
