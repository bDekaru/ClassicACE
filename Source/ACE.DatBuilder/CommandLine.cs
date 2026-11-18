using System;
using System.Collections.Generic;
using System.Linq;

namespace ACE.DatBuilder
{
    /// <summary>
    /// The options of one command: "--name value" options (an option may repeat), "--name" flags and at most one positional argument.
    /// Anything else is an error.
    /// </summary>
    public sealed class CommandLine
    {
        private readonly Dictionary<string, List<string>> values = new Dictionary<string, List<string>>();
        private readonly HashSet<string> flags = new HashSet<string>();

        /// <summary>The argument that is not an option, or null.</summary>
        public string Positional { get; private set; }

        /// <summary>Parses args[1..]; args[0] is the command name.</summary>
        public static CommandLine Parse(string[] args, string[] valueOptions, string[] flagOptions = null, bool allowPositional = false)
        {
            var line = new CommandLine();

            for (var i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (valueOptions.Contains(arg))
                {
                    if (i + 1 >= args.Length)
                        throw new ArgumentException($"{arg} needs a value.");

                    if (!line.values.TryGetValue(arg, out var list))
                        line.values[arg] = list = new List<string>();

                    list.Add(args[++i]);
                }
                else if (flagOptions != null && flagOptions.Contains(arg))
                    line.flags.Add(arg);
                else if (allowPositional && line.Positional == null && !arg.StartsWith("--"))
                    line.Positional = arg;
                else
                    throw new ArgumentException($"Unexpected argument: {arg}");
            }

            return line;
        }

        /// <summary>The last value given for the option, or null.</summary>
        public string Value(string option) => values.TryGetValue(option, out var list) ? list[list.Count - 1] : null;

        /// <summary>Every value given for the option, in order.</summary>
        public IReadOnlyList<string> Values(string option) => values.TryGetValue(option, out var list) ? list : (IReadOnlyList<string>)Array.Empty<string>();

        public bool Flag(string option) => flags.Contains(option);
    }
}
