namespace ACE.Server.Pathfinding.Reachability
{
    /// <summary>
    /// A place on the world graph: a piece of a landblock's outdoor summary (Group is Outdoors), or a piece of one of its
    /// interior cell groups. Piece is NoPiece where the place is on no piece.
    /// </summary>
    public readonly record struct GraphSpot(int Landblock, int Group, ushort Piece)
    {
        public const int Outdoors = -1;
    }

    public enum TravelKind : byte
    {
        /// <summary>
        /// Taken by the reference player (regular gameplay mode, NPK, not Olthoi) within its level limits: in the labels.
        /// </summary>
        Open,

        /// <summary>
        /// Needs a quest, or a kill a player may need a group for: kept for the report, not in the labels.
        /// </summary>
        Quest,

        /// <summary>
        /// An NPC sends the player or summons a portal, for a fee, an item, a quest or nothing: not judged yet, kept for
        /// the report.
        /// </summary>
        Unjudged
    }

    /// <summary>
    /// One-way travel from one graph spot to another: a portal and the like. A level of 0 sets no limit.
    /// </summary>
    public class TravelLink
    {
        public GraphSpot From { get; set; }

        public GraphSpot To { get; set; }

        public int MinLevel { get; set; }

        public int MaxLevel { get; set; }

        public TravelKind Kind { get; set; }

        /// <summary>
        /// What it is, for the report, e.g. "portal 1595 Abandoned Mine".
        /// </summary>
        public string Source { get; set; }
    }
}
