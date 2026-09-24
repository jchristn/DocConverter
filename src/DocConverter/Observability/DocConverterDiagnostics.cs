namespace DocConverter.Observability
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// ActivitySource and Meter for DocConverter, both named "DocConverter". Subscribe with an ActivityListener,
    /// MeterListener, or OpenTelemetry. Instrumentation is best effort and never throws into a conversion.
    /// Instruments:
    /// docconverter.conversions (counter; tags from, to, outcome),
    /// docconverter.conversion.duration (histogram, milliseconds; tags from, to),
    /// docconverter.input.bytes (histogram, bytes; tag from),
    /// docconverter.warnings (counter; tag code).
    /// </summary>
    public static class DocConverterDiagnostics
    {
        /// <summary>
        /// Name of the ActivitySource and the Meter.
        /// </summary>
        public const string Name = "DocConverter";

        /// <summary>
        /// Activity source. One activity named "docconverter.convert" is started per conversion.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(Name, Version);

        /// <summary>
        /// Meter.
        /// </summary>
        public static readonly Meter Meter = new Meter(Name, Version);

        private static readonly Counter<long> _Conversions = Meter.CreateCounter<long>("docconverter.conversions", "{conversion}", "Conversions attempted, by source, target and outcome.");
        private static readonly Histogram<double> _Duration = Meter.CreateHistogram<double>("docconverter.conversion.duration", "ms", "Conversion duration.");
        private static readonly Histogram<long> _InputBytes = Meter.CreateHistogram<long>("docconverter.input.bytes", "By", "Input size.");
        private static readonly Counter<long> _Warnings = Meter.CreateCounter<long>("docconverter.warnings", "{warning}", "Warnings raised, by code.");

        /// <summary>
        /// Library version reported by the instruments.
        /// </summary>
        public static string Version
        {
            get
            {
                Version? v = typeof(DocConverterDiagnostics).Assembly.GetName().Version;
                return v == null ? "0.0.0" : v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        internal static Activity? StartConvert(string from, string to)
        {
            try
            {
                Activity? activity = ActivitySource.StartActivity("docconverter.convert", ActivityKind.Internal);
                if (activity != null)
                {
                    activity.SetTag("docconverter.from", from);
                    activity.SetTag("docconverter.to", to);
                }

                return activity;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void RecordConversion(string from, string to, string outcome, double durationMs, long inputBytes)
        {
            try
            {
                _Conversions.Add(1, new KeyValuePair<string, object?>("from", from), new KeyValuePair<string, object?>("to", to), new KeyValuePair<string, object?>("outcome", outcome));
                _Duration.Record(durationMs, new KeyValuePair<string, object?>("from", from), new KeyValuePair<string, object?>("to", to));
                _InputBytes.Record(inputBytes, new KeyValuePair<string, object?>("from", from));
            }
            catch (Exception)
            {
                // Instrumentation must never break a conversion.
            }
        }

        internal static void RecordWarning(string code, int count)
        {
            try
            {
                _Warnings.Add(count, new KeyValuePair<string, object?>("code", code));
            }
            catch (Exception)
            {
                // Instrumentation must never break a conversion.
            }
        }
    }
}
