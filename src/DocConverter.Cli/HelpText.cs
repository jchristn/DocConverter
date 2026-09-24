namespace DocConverter.Cli
{
    /// <summary>
    /// The docconv help text.
    /// </summary>
    public static class HelpText
    {
        /// <summary>
        /// Full usage text, ending with a newline.
        /// </summary>
        public const string Text =
@"docconv: convert documents between formats, in memory, for humans and agents.

USAGE:
    docconv convert -i <path|-> -o <path|-> [--from <format>] [--to <format>] [options]
    docconv detect  -i <path|-> [--json]
    docconv formats [--json]
    docconv --help | -h | -? | /?
    docconv --version | -v

    '-' means stdin for -i and stdout for -o. Documents travel as raw bytes, so binary
    formats survive pipes. Options accept '--opt value' or '--opt=value'.

FORMATS:
    text (txt), markdown (md), html (htm), json, xml, csv, tsv, rtf, docx (word),
    xlsx (excel), pptx (powerpoint), pdf, png, jpeg (jpg), gif, bmp, tiff (tif), webp.
    rtf and the image formats are inputs only. Run 'docconv formats' for the full matrix.

CONVERT:
    -i, --input <path|->          Input file, or - for stdin (required)
    -o, --output <path|->         Output file, or - for stdout (required)
        --from <format>           Source format. Omitted: detected from content, using the
                                  input file extension as a hint
        --to <format>             Target format. Omitted: taken from the output extension.
                                  Required when -o is -
        --overwrite               Replace an existing output file
        --options <file.json>     Load conversion options from a JSON file; flags override it
        --title <text>            Title written into targets that carry one
        --no-images               Drop all images
        --images <mode>           embed, omit, placeholder or external (Markdown and HTML).
                                  external writes image files next to the output file
        --no-metadata             Do not carry title, author and other metadata
        --input-encoding <name>   Encoding of text input (default: byte order mark, then utf-8)
        --output-encoding <name>  Encoding of text output (default: utf-8 without BOM)
        --line-ending <lf|crlf|platform>
        --deterministic           Pin timestamps and ids so identical input gives identical bytes
        --strict                  Treat warnings as errors: exit 5 and remove the output file
        --csv-delimiter <char>    CSV or TSV delimiter, a single character or 'tab'
        --no-header               CSV or TSV input has no header row
        --table <first|all|n>     Which tables to write to CSV or TSV
        --html-fragment           Write body content only, without html, head and body
        --html-no-css             Omit the embedded stylesheet
        --text-wrap <n>           Wrap plain text at n columns (0, or 20 to 1000)
        --json-compact            Write JSON without indentation
        --json-no-binary          Leave image bytes out of JSON
        --page-size <a4|letter|legal>   PDF and DOCX page size
        --margin <points>         PDF and DOCX page margin in points (0 to 216)
        --slide-split <1-6>       PPTX: start a new slide at headings of this level or lower
        --include-notes           PPTX input: include speaker notes
        --include-hidden-sheets   XLSX input: include hidden sheets
        --preserve-pages          PDF input: keep each page as its own section
        --max-input-mb <n>        Largest accepted input in megabytes (default 256)
        --json                    Write a one line JSON report (see REPORTS)
    -q, --quiet                   Suppress the summary line (errors still go to stderr)

DETECT:
    -i, --input <path|->          Input to examine
        --json                    Write the detection as JSON
        --max-input-mb <n>        Largest accepted input in megabytes

REPORTS:
    stdout carries only the converted document (with -o -) or the JSON report (with --json and
    a file output). Everything else, including the JSON report when stdout carries the document,
    goes to stderr. The report has contractVersion, success, command, input, output, durationMs,
    warnings, statistics, error and exitCode.

EXIT CODES:
    0    Success (warnings allowed unless --strict)
    1    The input could not be read or the output could not be written
    2    Usage error: unknown option, missing argument or invalid value
    3    Unsupported: format not recognized or not supported, or pair not supported
    4    I/O error: input not found, output exists without --overwrite, access denied
    5    Warnings raised under --strict
    6    Input exceeds --max-input-mb
    130  Cancelled

EXAMPLES (for agents):
    docconv convert -i report.docx -o report.md --json
        Converts to Markdown and prints a JSON report on stdout.
    docconv convert -i scan.pdf -o - --to md --quiet
        Streams Markdown to stdout; nothing else is written there.
    cat page.html | docconv convert -i - -o - --from html --to txt --strict
        Fails with exit 5 instead of silently losing content.
";
    }
}
