using System;

namespace ACE.Entity.Enum
{
    public enum GameplayModes
    {
        Regular         = 0,
        Limbo           = 1,
        SoloSelfFound   = 5000,
        HardcoreNPK     = 10000,
        HardcorePK      = 20000,
        InitialMode     = int.MaxValue,
    }
}
