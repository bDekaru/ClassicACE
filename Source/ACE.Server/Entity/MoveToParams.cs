using System;

using ACE.Server.WorldObjects;

namespace ACE.Server.Entity
{
    public class MoveToParams
    {
        public Action<bool> Callback;

        public WorldObject Target;

        public float? UseRadius;

        public ACE.Entity.Position TargetPosition;

        public MoveToParams(Action<bool> callback, WorldObject target, float? useRadius = null)
        {
            Callback = callback;

            Target = target;

            UseRadius = useRadius;
        }

        public MoveToParams(Action<bool> callback, ACE.Entity.Position target, float? radius = null)
        {
            Callback = callback;

            TargetPosition = target;

            UseRadius = radius;
        }
    }
}
