using System;

namespace ACE.DatLoader
{
    static class RemapExtensions
    {
        /// <summary>
        /// Maps an id that is set. A 0 means "none" in the dat and stays 0.
        /// </summary>
        public static uint RemapIfSet(this Func<uint, uint> map, uint id)
        {
            return id != 0 ? map(id) : 0;
        }
    }
}
