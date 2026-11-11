using EnvCell = ACE.DatLoader.FileTypes.EnvCell;

namespace ACE.Server.Pathfinding.Geometry
{
    /// <summary>
    /// An interior cell of a cell group (see LandblockGeometry.CellGroups).
    /// </summary>
    public class CellGeometry
    {
        /// <summary>
        /// The id of this landcell in the format of 0xAAAABBBB where AAAA is the landblock
        /// id and BBBB is the cell id
        /// </summary>
        public uint CellId { get; }

        /// <summary>
        /// The cell from the dat, as read when the cell groups were formed.
        /// </summary>
        public EnvCell EnvCell { get; }

        public CellGeometry(uint cellId, EnvCell envCell)
        {
            CellId = cellId;
            EnvCell = envCell;
        }

        internal static bool ShouldIgnoreEnvironment(uint environmentId)
        {
            switch (environmentId)
            {
                // roof tiles with "walkable" ceilings. the normal of the top ceiling poly is facing up
                case 0x0D0000CA:
                case 0x0D00016D:
                    return true;
                default:
                    return false;
            }
        }
    }
}
