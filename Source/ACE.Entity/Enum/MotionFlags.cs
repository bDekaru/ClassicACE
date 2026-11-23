using System;

namespace ACE.Entity.Enum
{
    [Flags]
    public enum MotionFlags : byte
    {
        None                = 0x0,
        StickToObject       = 0x1,
        StandingLongJump    = 0x2,

        /// <summary>
        /// Not part of retail. Tells a client that supports it(CustomdDM) to drop the object's queued link animations before applying this
        /// movement, so a running animation is cut short. The other clients do this automatically for the Dead motion and nothing else.
        /// </summary>
        AnimationBreak      = 0x4,

        /// <summary>
        /// Not part of retail. On a MoveTo for a creature that is already running to its target (CustomDM clients): keep the move going and only
        /// apply the new run rate it carries, instead of cancelling the move and starting it again.
        /// </summary>
        SpeedUpdate         = 0x8
    };
}
