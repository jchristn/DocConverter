namespace DocConverter.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A parsed docconv command line.
    /// </summary>
    public class CliArguments
    {
        private Dictionary<string, string> _Values = new Dictionary<string, string>(StringComparer.Ordinal);
        private HashSet<string> _Flags = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// The command. Default None.
        /// </summary>
        public CliCommandEnum Command { get; set; } = CliCommandEnum.None;

        /// <summary>
        /// Valued options keyed by long name, for example "--input". Never null.
        /// </summary>
        public Dictionary<string, string> Values
        {
            get => _Values;
            set => _Values = value ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Flag options that are set, by long name, for example "--overwrite". Never null.
        /// </summary>
        public HashSet<string> Flags
        {
            get => _Flags;
            set => _Flags = value ?? new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Input path, or "-" for stdin. Null when not given.
        /// </summary>
        public string? Input
        {
            get => Get("--input");
        }

        /// <summary>
        /// Output path, or "-" for stdout. Null when not given.
        /// </summary>
        public string? Output
        {
            get => Get("--output");
        }

        /// <summary>
        /// True when --json was given.
        /// </summary>
        public bool Json
        {
            get => _Flags.Contains("--json");
        }

        /// <summary>
        /// True when --quiet was given.
        /// </summary>
        public bool Quiet
        {
            get => _Flags.Contains("--quiet");
        }

        /// <summary>
        /// Instantiate empty arguments.
        /// </summary>
        public CliArguments()
        {
        }

        /// <summary>
        /// Value of a valued option, or null when not given.
        /// </summary>
        /// <param name="name">Long option name, for example "--from".</param>
        /// <returns>Value or null.</returns>
        public string? Get(string name)
        {
            return _Values.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// True when a flag option is set.
        /// </summary>
        /// <param name="name">Long option name, for example "--overwrite".</param>
        /// <returns>True when set.</returns>
        public bool Has(string name)
        {
            return _Flags.Contains(name);
        }
    }
}
