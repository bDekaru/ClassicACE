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
            "bake [threads] | verify [samples] | stop | status | at | report\n" +
            "bake: builds the summaries still missing and puts the graph in use (threads: default all cores)\n" +
            "verify: checks the summaries against Detour's links and against Recast tiles of sampled terrain landblocks, on all cores (samples: default 150, a few minutes)\n" +
            "stop: stops a running bake or verify after the work in progress; the next bake resumes there\n" +
            "status: the progress of a bake or verify, and the graph in use\n" +
            "at: what the graph says about where you stand\n" +
            "report: label totals, start spots, Unknown landblocks and the largest pieces that aren't Walkable, written to a file")]
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
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.StartReachabilityBake(threads) ? $"Reachability bake started on {threads} threads." : "A reachability bake or verify is already running.");
                    break;

                case "verify":
                    var samples = Pathfinder.DefaultVerifySamples;
                    if (parameters.Length > 1 && (!int.TryParse(parameters[1], out samples) || samples < 0))
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "Samples must be a number of 0 or more.");
                        return;
                    }
                    if (!Pathfinder.HasReachabilityGraph)
                    {
                        CommandHandlerHelper.WriteOutputInfo(session, "There is no reachability graph yet: run @reachability bake first.");
                        return;
                    }
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.StartReachabilityVerify(samples, System.Environment.ProcessorCount)
                        ? $"Reachability verify started; @reachability status shows its progress, the results go to {Pathfinder.ReachabilityVerifyPath}."
                        : "A reachability bake or verify is already running.");
                    break;

                case "stop":
                    CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.StopReachabilityBake() ? "The reachability bake or verify stops after the work in progress." : "No reachability bake or verify is running.");
                    break;

                case "report":
                    // a pass over the whole world: off the command thread
                    System.Threading.Tasks.Task.Run(() => CommandHandlerHelper.WriteOutputInfo(session, Pathfinder.WriteReachabilityReport() ?? "There is no reachability graph yet: run @reachability bake first."));
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
                    CommandHandlerHelper.WriteOutputInfo(session, "Usage: @reachability bake [threads] | verify [samples] | stop | status | at | report");
                    break;
            }
        }
    }
}
