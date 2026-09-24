namespace DocConverter.Cli
{
    using System;
    using System.IO;
    using DocConverter.Exceptions;

    /// <summary>
    /// Maps exceptions to exit codes and report error codes. The single place that decides what each failure means.
    /// </summary>
    public static class CliErrors
    {
        /// <summary>
        /// Exit code for an exception.
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>Exit code.</returns>
        public static CliExitCodeEnum ExitCode(Exception ex)
        {
            if (ex is OperationCanceledException) return CliExitCodeEnum.Cancelled;
            if (ex is CliUsageException) return CliExitCodeEnum.Usage;
            if (ex is InvalidConversionOptionsException) return CliExitCodeEnum.Usage;
            if (ex is ConversionWarningException) return CliExitCodeEnum.Warnings;
            if (ex is InputTooLargeException) return CliExitCodeEnum.InputTooLarge;
            if (ex is UnsupportedFormatException) return CliExitCodeEnum.Unsupported;
            if (ex is ConversionNotSupportedException) return CliExitCodeEnum.Unsupported;
            if (ex is DocumentReadException) return CliExitCodeEnum.ConversionFailed;
            if (ex is DocumentWriteException) return CliExitCodeEnum.ConversionFailed;
            if (ex is NotImplementedException) return CliExitCodeEnum.ConversionFailed;
            if (ex is FileNotFoundException) return CliExitCodeEnum.IO;
            if (ex is DirectoryNotFoundException) return CliExitCodeEnum.IO;
            if (ex is UnauthorizedAccessException) return CliExitCodeEnum.IO;
            if (ex is IOException) return CliExitCodeEnum.IO;
            if (ex is ArgumentException) return CliExitCodeEnum.Usage;
            return CliExitCodeEnum.ConversionFailed;
        }

        /// <summary>
        /// Report error code for an exception.
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>Error code.</returns>
        public static string ErrorCode(Exception ex)
        {
            if (ex is OperationCanceledException) return "Cancelled";
            if (ex is CliUsageException) return "Usage";
            if (ex is InvalidConversionOptionsException) return "InvalidOptions";
            if (ex is ConversionWarningException) return "Warnings";
            if (ex is InputTooLargeException) return "InputTooLarge";
            if (ex is UnsupportedFormatException) return "UnsupportedFormat";
            if (ex is ConversionNotSupportedException) return "ConversionNotSupported";
            if (ex is DocumentReadException) return "DocumentRead";
            if (ex is DocumentWriteException) return "DocumentWrite";
            if (ex is NotImplementedException) return "NotImplemented";
            if (ex is CliOutputExistsException) return "OutputExists";
            if (ex is FileNotFoundException) return "NotFound";
            if (ex is DirectoryNotFoundException) return "NotFound";
            if (ex is UnauthorizedAccessException) return "AccessDenied";
            if (ex is IOException) return "IO";
            if (ex is ArgumentException) return "Usage";
            return "Internal";
        }

        /// <summary>
        /// Message for an exception, suitable for one line of stderr.
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>Message.</returns>
        public static string Message(Exception ex)
        {
            if (ex is OperationCanceledException) return "Cancelled.";
            string message = ex.Message.Replace("\r", " ").Replace("\n", " ");
            return message;
        }
    }
}
