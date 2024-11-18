using System.Collections.Generic;
using System.Numerics;

namespace ACE.Server.Pathfinding.Geometry
{
    public class BoundingBox {
        private readonly Vector3 _maxVector3 = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        private readonly Vector3 _minVector3 = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        private Vector3 _minBounds = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        private Vector3 _maxBounds = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        public Vector3 MinBounds => _minBounds == _maxVector3 ? Vector3.Zero : _minBounds;
        public Vector3 MaxBounds => _maxBounds == _minVector3 ? Vector3.Zero : _maxBounds;

        public Vector3 Size => MaxBounds - MinBounds;
        public Vector3 Center => MinBounds + Size / 2;

        public BoundingBox() {

        }

        public BoundingBox(Vector3 minBounds, Vector3 maxBounds) {
            AddVector(minBounds);
            AddVector(maxBounds);
        }

        public BoundingBox(IEnumerable<Vector3> vectors) {
            AddVectors(vectors);
        }

        public BoundingBox(IEnumerable<BoundingBox> boxes) {
            foreach (var box in boxes) {
                AddVector(box.MinBounds);
                AddVector(box.MaxBounds);
            }
        }

        public void AddVectors(IEnumerable<Vector3> vectors) {
            foreach (var vector in vectors) {
                AddVector(vector);
            }
        }

        public void AddVector(Vector3 vector) {
            if (vector.X < _minBounds.X) _minBounds.X = vector.X;
            if (vector.Y < _minBounds.Y) _minBounds.Y = vector.Y;
            if (vector.Z < _minBounds.Z) _minBounds.Z = vector.Z;

            if (vector.X > _maxBounds.X) _maxBounds.X = vector.X;
            if (vector.Y > _maxBounds.Y) _maxBounds.Y = vector.Y;
            if (vector.Z > _maxBounds.Z) _maxBounds.Z = vector.Z;
        }

        public override string ToString() {
            return $"BoundingBox<Min:{MinBounds} Max:{MaxBounds} Size: {Size} Center: {Center}>";
        }
    }
}
