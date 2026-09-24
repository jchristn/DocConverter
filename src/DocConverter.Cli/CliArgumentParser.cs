namespace DocConverter.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Parses the docconv command line by hand (no parsing library). Accepts "--opt value" and "--opt=value", treats "--"
    /// as the end of options, rejects unknown options and options that do not apply to the command. Thread safe.
    /// </summary>
    public static class CliArgumentParser
    {
        private static readonly Dictionary<string, string> _Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "-i", "--input" },
            { "-o", "--output" },
            { "-q", "--quiet" },
            { "-h", "--help" },
            { "-?", "--help" },
            { "/?", "--help" },
            { "-v", "--version" }
        };

        private static readonly HashSet<string> _Valued = new HashSet<string>(StringComparer.Ordinal)
        {
            "--input", "--output", "--from", "--to", "--options", "--title", "--images", "--input-encoding",
            "--output-encoding", "--line-ending", "--csv-delimiter", "--table", "--text-wrap", "--page-size", "--margin",
            "--slide-split", "--max-input-mb"
        };

        private static readonly HashSet<string> _Flags = new HashSet<string>(StringComparer.Ordinal)
        {
            "--overwrite", "--no-images", "--no-metadata", "--deterministic", "--strict", "--no-header", "--html-fragment",
            "--html-no-css", "--json-compact", "--json-no-binary", "--include-notes", "--include-hidden-sheets",
            "--preserve-pages", "--json", "--quiet", "--help", "--version"
        };

        private static readonly HashSet<string> _DetectOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "--input", "--json", "--quiet", "--max-input-mb"
        };

        private static readonly HashSet<string> _FormatsOptions = new HashSet<string>(StringComparer.Ordinal)
        {
            "--json", "--quiet"
        };

        /// <summary>
        /// Parse the command line.
        /// </summary>
        /// <param name="args">Arguments, without the program name.</param>
        /// <returns>Parsed arguments. Command is Help or Version when those were requested anywhere.</returns>
        /// <exception cref="ArgumentNullException">Thrown when args is null.</exception>
        /// <exception cref="CliUsageException">Thrown when the command line is invalid.</exception>
        public static CliArguments Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            CliArguments result = new CliArguments();
            if (args.Length == 0) return result;

            foreach (string arg in args)
            {
                if (arg == "--") break;
                string name = Canonical(SplitName(arg));
                if (name == "--help")
                {
                    result.Command = CliCommandEnum.Help;
                    return result;
                }
            }

            if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v" || args[0] == "/version"))
            {
                result.Command = CliCommandEnum.Version;
                return result;
            }

            int start = 0;
            string first = args[0];
            if (first == "convert") result.Command = CliCommandEnum.Convert;
            else if (first == "detect") result.Command = CliCommandEnum.Detect;
            else if (first == "formats") result.Command = CliCommandEnum.Formats;
            else if (first == "help") result.Command = CliCommandEnum.Help;
            else if (first == "version") result.Command = CliCommandEnum.Version;
            else if (first.StartsWith("-", StringComparison.Ordinal))
            {
                string name = Canonical(SplitName(first));
                if (name == "--version")
                {
                    result.Command = CliCommandEnum.Version;
                    return result;
                }

                throw new CliUsageException("Missing command. Expected convert, detect or formats before '" + first + "'.");
            }
            else throw new CliUsageException("Unknown command '" + first + "'. Expected convert, detect or formats.");

            start = 1;
            if (result.Command == CliCommandEnum.Help || result.Command == CliCommandEnum.Version) return result;

            bool optionsEnded = false;
            for (int i = start; i < args.Length; i++)
            {
                string arg = args[i];
                if (!optionsEnded && arg == "--")
                {
                    optionsEnded = true;
                    continue;
                }

                if (optionsEnded || arg == "-" || !arg.StartsWith("-", StringComparison.Ordinal))
                    throw new CliUsageException("Unexpected argument '" + arg + "'. Pass paths with -i and -o.");

                string rawName = SplitName(arg);
                string name = Canonical(rawName);
                string? inlineValue = null;
                int eq = arg.IndexOf('=');
                if (eq > 0 && arg.StartsWith("--", StringComparison.Ordinal)) inlineValue = arg.Substring(eq + 1);

                if (name == "--version")
                {
                    result.Command = CliCommandEnum.Version;
                    return result;
                }

                if (_Valued.Contains(name))
                {
                    EnsureApplies(result.Command, name, arg);
                    string value;
                    if (inlineValue != null) value = inlineValue;
                    else
                    {
                        if (i + 1 >= args.Length) throw new CliUsageException("Option '" + rawName + "' requires a value.");
                        value = args[++i];
                    }

                    result.Values[name] = value;
                }
                else if (_Flags.Contains(name))
                {
                    EnsureApplies(result.Command, name, arg);
                    bool on = true;
                    if (inlineValue != null) on = ParseBool(inlineValue, rawName);
                    if (on) result.Flags.Add(name);
                    else result.Flags.Remove(name);
                }
                else
                {
                    throw new CliUsageException("Unknown option '" + rawName + "'. Run docconv --help for the list of options.");
                }
            }

            return result;
        }

        private static void EnsureApplies(CliCommandEnum command, string name, string arg)
        {
            if (command == CliCommandEnum.Detect && !_DetectOptions.Contains(name))
                throw new CliUsageException("Option '" + SplitName(arg) + "' does not apply to the detect command.");
            if (command == CliCommandEnum.Formats && !_FormatsOptions.Contains(name))
                throw new CliUsageException("Option '" + SplitName(arg) + "' does not apply to the formats command.");
        }

        private static string SplitName(string arg)
        {
            int eq = arg.IndexOf('=');
            if (eq > 0 && arg.StartsWith("--", StringComparison.Ordinal)) return arg.Substring(0, eq);
            return arg;
        }

        private static string Canonical(string name)
        {
            return _Aliases.TryGetValue(name, out string? canonical) ? canonical : name;
        }

        private static bool ParseBool(string value, string option)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "on":
                    return true;
                case "false":
                case "0":
                case "no":
                case "off":
                    return false;
                default:
                    throw new CliUsageException("Option '" + option + "' expects true or false; got '" + value + "'.");
            }
        }
    }
}
