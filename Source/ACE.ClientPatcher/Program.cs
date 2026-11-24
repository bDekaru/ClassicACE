using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using ACE.DatBuilder;

namespace ACE.ClientPatcher
{
    public static class Program
    {
        private const int Ok = 0;
        private const int UsageError = 1;
        private const int PatchFailure = 2;

        private const string Usage =
@"ACE.ClientPatcher patch --in <End of Retail acclient.exe> --out <patched exe> [--client customdm|infiltration] [--entry <id>]... [--force]
ACE.ClientPatcher list [--markdown] [--out <file>]
ACE.ClientPatcher check --in <client exe> [--client customdm|infiltration]

  check   reports, for each change of the catalogue that the client has, whether the exe has it (applied), does not (original), or has other bytes there (not both: another change of the catalogue rewrote them, or it is another client).
  list --markdown  writes the catalogue as the documentation page (to --out, default the console).
  patch   applies every entry of the catalogue that the client has to the retail exe (End of Retail 11.6096, checked by MD5 and SHA-256) and writes the patched exe to --out.
  --client  the client to build (default customdm)
  --entry   apply only the entry of that id (repeatable; the ids are listed by 'list'), to find out what an entry contributes
  --force   replace an existing output file
  list    prints the entries, their changes and the clients that have each one.";

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    Console.WriteLine(Usage);
                    return UsageError;
                }

                switch (args[0].ToLowerInvariant())
                {
                    case "patch": return Patch(args);
                    case "list": return List(args);
                    case "check": return Check(args);

                    default:
                        Console.WriteLine(Usage);
                        return UsageError;
                }
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return UsageError;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is FormatException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
                return PatchFailure;
            }
        }

        private static int Patch(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--in", "--out", "--client", "--entry" }, new[] { "--force" });
            var input = line.Value("--in") ?? throw new ArgumentException("--in is required.");
            var output = line.Value("--out") ?? throw new ArgumentException("--out is required.");

            var variant = ParseClient(line);

            if (Path.GetFullPath(input) == Path.GetFullPath(output))
                throw new ArgumentException("--out must not be the input file.");

            var entries = PatchCatalogue.Load();
            var wanted = line.Values("--entry");

            var unknown = wanted.FirstOrDefault(id => entries.All(entry => entry.Id != id));

            if (unknown != null)
                throw new ArgumentException($"Unknown entry: {unknown}");

            var exe = ClientExe.LoadRetail(input);

            foreach (var entry in entries.Where(entry => wanted.Count == 0 || wanted.Contains(entry.Id)))
            {
                var count = 0;

                foreach (var edit in entry.EditsFor(variant))
                {
                    exe.Apply(edit);
                    count++;
                }

                Console.WriteLine($"{entry.Id}: {count} edits");
            }

            exe.Save(output, line.Flag("--force"));
            Console.WriteLine($"Wrote {output}");
            return Ok;
        }

        private static ClientVariant ParseClient(CommandLine line)
        {
            var name = line.Value("--client") ?? nameof(ClientVariant.CustomDM);

            if (!Enum.TryParse<ClientVariant>(name, true, out var variant) || !Enum.IsDefined(variant))
                throw new ArgumentException($"Unknown client: {name}");

            return variant;
        }

        private static int List(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--out" }, new[] { "--markdown" });
            var entries = PatchCatalogue.Load();

            if (line.Flag("--markdown"))
            {
                var page = Markdown(entries);
                var path = line.Value("--out");

                if (path != null)
                    File.WriteAllText(path, page);
                else
                    Console.Write(page);

                return Ok;
            }

            foreach (var entry in entries)
            {
                Console.WriteLine($"{entry.Id}: {entry.Title}");

                foreach (var item in entry.Items)
                    Console.WriteLine($"  [{string.Join(", ", item.Variants)}] {item.Change} ({item.Edits.Count} edits)");
            }

            return Ok;
        }

        private static int Check(string[] args)
        {
            var line = CommandLine.Parse(args, new[] { "--in", "--client" });
            var input = line.Value("--in") ?? throw new ArgumentException("--in is required.");

            var variant = ParseClient(line);

            var exe = ClientExe.Load(input);
            var entries = PatchCatalogue.Load();
            var all = entries.SelectMany(entry => entry.EditsFor(variant)).ToList();

            foreach (var entry in entries)
            {
                Console.WriteLine($"{entry.Id}: {entry.Title}");

                foreach (var item in entry.Items.Where(item => item.Variants.Contains(variant)))
                {
                    // an edit that shares bytes with another edit of the catalogue (a dependent change) cannot be judged on its own
                    var states = item.EditsFor(variant).Where(edit => !all.Any(other => other != edit && Overlap(edit, other))).Select(exe.Inspect).ToList();
                    Console.WriteLine($"  {Summarize(states),-22} {item.Change}");
                }
            }

            return Ok;
        }

        private static string Summarize(List<EditState> states)
        {
            var applied = states.Count(state => state == EditState.Applied);

            if (states.Count == 0)
                return "shares bytes with another";

            if (applied == states.Count)
                return "applied";

            return states.All(state => state == EditState.Original) ? "original" : $"{applied} of {states.Count} edits applied";
        }

        private static bool Overlap(PatchEdit first, PatchEdit second)
        {
            return first.Address != null && second.Address != null && first.Va < second.Va + second.Length && second.Va < first.Va + first.Length;
        }

        private static string Markdown(List<PatchEntry> entries)
        {
            var page = new StringBuilder();
            page.AppendLine("# Client patches");
            page.AppendLine();
            page.AppendLine($"Generated by `ACE.ClientPatcher list --markdown`; the data is in `Patches/Data`. Input: the End of Retail acclient.exe (11.6096), MD5 `{ClientExe.RetailMd5}`, SHA-256 `{ClientExe.RetailSha256}`. Entries apply in the order below.");

            foreach (var entry in entries)
            {
                page.AppendLine().AppendLine($"## {entry.Title} (`{entry.Id}`)").AppendLine().AppendLine(entry.Description);

                foreach (var item in entry.Items)
                {
                    page.AppendLine().AppendLine($"- **{item.Change}** ({string.Join(", ", item.Variants)})");

                    foreach (var edit in item.Edits)
                        page.AppendLine($"  - `{edit.Address ?? "file " + edit.FileOffset}`{(edit.Variant != null ? $" ({edit.Variant} only)" : "")}, {edit.Length} bytes: {edit.Note}");
                }
            }

            return page.ToString();
        }
    }
}
