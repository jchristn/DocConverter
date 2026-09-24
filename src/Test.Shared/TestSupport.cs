namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Assertion and fixture helpers shared by every suite. No console output.
    /// </summary>
    public static class TestSupport
    {
        /// <summary>
        /// Throw when the condition is false.
        /// </summary>
        /// <param name="condition">Condition that must hold.</param>
        /// <param name="message">Failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the condition is false.</exception>
        public static void Assert(bool condition, string message)
        {
            if (!condition) throw new TestAssertionException(message);
        }

        /// <summary>
        /// Assert two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="message">Failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the values differ.</exception>
        public static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestAssertionException(message + " (expected '" + expected + "', got '" + actual + "')");
        }

        /// <summary>
        /// Assert that text contains a fragment (ordinal).
        /// </summary>
        /// <param name="text">Text to search.</param>
        /// <param name="fragment">Fragment that must appear.</param>
        /// <param name="message">Failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the fragment is missing.</exception>
        public static void AssertContains(string? text, string fragment, string message)
        {
            if (text == null || text.IndexOf(fragment, StringComparison.Ordinal) < 0)
                throw new TestAssertionException(message + " (missing '" + fragment + "' in: " + Truncate(text, 400) + ")");
        }

        /// <summary>
        /// Assert that text does not contain a fragment (ordinal).
        /// </summary>
        /// <param name="text">Text to search.</param>
        /// <param name="fragment">Fragment that must not appear.</param>
        /// <param name="message">Failure message.</param>
        /// <exception cref="TestAssertionException">Thrown when the fragment is present.</exception>
        public static void AssertNotContains(string? text, string fragment, string message)
        {
            if (text != null && text.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                throw new TestAssertionException(message + " (unexpected '" + fragment + "' in: " + Truncate(text, 400) + ")");
        }

        /// <summary>
        /// Assert that the action throws an exception of the given type (or a subtype).
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="message">Failure message.</param>
        /// <returns>The exception.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception or a different type is thrown.</exception>
        public static TException ExpectThrows<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(message + " (expected " + typeof(TException).Name + ", got " + ex.GetType().Name + ": " + ex.Message + ")", ex);
            }

            throw new TestAssertionException(message + " (no exception thrown)");
        }

        /// <summary>
        /// Assert that the asynchronous action throws an exception of the given type (or a subtype).
        /// </summary>
        /// <typeparam name="TException">Expected exception type.</typeparam>
        /// <param name="action">Action expected to throw.</param>
        /// <param name="message">Failure message.</param>
        /// <returns>The exception.</returns>
        /// <exception cref="TestAssertionException">Thrown when no exception or a different type is thrown.</exception>
        public static async Task<TException> ExpectThrowsAsync<TException>(Func<Task> action, string message) where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(message + " (expected " + typeof(TException).Name + ", got " + ex.GetType().Name + ": " + ex.Message + ")", ex);
            }

            throw new TestAssertionException(message + " (no exception thrown)");
        }

        /// <summary>
        /// Read an embedded fixture by its path under Fixtures/, for example "Text/sample.md".
        /// </summary>
        /// <param name="relativePath">Path relative to the Fixtures folder, with forward slashes.</param>
        /// <returns>Fixture bytes.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the fixture does not exist.</exception>
        public static byte[] Fixture(string relativePath)
        {
            string name = "Fixtures/" + relativePath.Replace('\\', '/');
            Assembly assembly = typeof(TestSupport).Assembly;
            string? actual = ResolveResourceName(name);
            using (Stream? stream = actual == null ? null : assembly.GetManifestResourceStream(actual))
            {
                if (stream == null) throw new FileNotFoundException("Embedded fixture not found: " + name);
                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Read an embedded text fixture as UTF-8.
        /// </summary>
        /// <param name="relativePath">Path relative to the Fixtures folder.</param>
        /// <returns>Fixture text.</returns>
        public static string FixtureText(string relativePath)
        {
            byte[] bytes = Fixture(relativePath);
            return new UTF8Encoding(false).GetString(bytes).TrimStart('﻿');
        }

        /// <summary>
        /// True when an embedded fixture exists.
        /// </summary>
        /// <param name="relativePath">Path relative to the Fixtures folder.</param>
        /// <returns>True when present.</returns>
        public static bool FixtureExists(string relativePath)
        {
            string name = "Fixtures/" + relativePath.Replace('\\', '/');
            return ResolveResourceName(name) != null;
        }

        private static string? ResolveResourceName(string normalizedName)
        {
            // Resource names carry the build machine's directory separator from %(RecursiveDir); compare normalized.
            foreach (string resource in typeof(TestSupport).Assembly.GetManifestResourceNames())
                if (resource.Replace('\\', '/') == normalizedName) return resource;
            return null;
        }

        /// <summary>
        /// Shorten text for failure messages.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="max">Maximum length.</param>
        /// <returns>Shortened text.</returns>
        public static string Truncate(string? text, int max)
        {
            if (text == null) return "(null)";
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }
    }
}
