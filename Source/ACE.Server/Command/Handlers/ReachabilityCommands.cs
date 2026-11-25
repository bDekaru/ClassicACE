using ACE.Common;
using ACE.Entity.Enum;
using ACE.Server.Network;
using ACE.Server.Pathfinding;

namespace ACE.Server.Command.Handlers
{
    public static class ReachabilityCommands
    {
        [CommandHandler("reachability", AccessLevel.Developer, CommandHandlerFlag.None, 1,
            "CustomDM: Runs the world reachability bake, or shows where it stands or what it says about your spot.",
            "bake [threads] | stop | status | at\n" +
            "bake: builds the summaries still missing and puts the graph in use (threads: default all cores)\n" +
            "stop: stops a running bake after the tiles in progress; the next bake resumes there\n" +
            "status: the bake's progress and the graph in use\n" +
            "at: what the graph says about where you stand")]
        public static void HandleReachability(Session session, params string[] parameters)
        {
            if (ConfigManager.Config.Server.WorldRuleset != Ruleset.CustomDM)
            {
                CommandHandlerHelper.WriteOutputInfo(session, "The reachability graph is CustomDM only.");
                return;
            }

            switch (parameters[0].ToLowerInvariant())
            {
                case "bake":
                    var threads = System.Environment.ProcessorCount;
                    if (parameters.Length > 1 && (!int.TryParse(parameters[1], out threads) || threads < 1 || threads > System.Environment.ProcessorCount))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, $"Threads must be a number from 1 to {System.Environment.ProcessorCount}.");
                        return;
                    }
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.StartReachabilityBake(threads) ? $"Reachability bake started on {threads} threads." : "A reachability bake is already running.");
                    break;

                case "stop":
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.StopReachabilityBake() ? "The reachability bake stops after the tiles in progress." : "No reachability bake is running.");
                    break;

                case "status":
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.GetReachabilityStatus());
                    break;

                case "at":
                    if (session?.Player == null)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "@reachability at needs a character in the world.");
                        return;
                    }
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.DescribeReachability(session.Player.Location));
                    break;

                default:
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: @reachability bake [threads] | stop | status | at");
                    break;
            }
        }
    }
}
