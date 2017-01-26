// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LogUtility.cs" company="Sean McElroy">
//   Copyright Sean McElroy; released as open-source software under the licensing terms of the MIT License.
// </copyright>
// <summary>
//   A set of utility methods for using additional functionality of the logging mechanism.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mystiko
{
    using System;

    using JetBrains.Annotations;

    using log4net;

    /// <summary>
    /// A set of utility methods for using additional functionality of the logging mechanism.
    /// </summary>
    internal static class LogUtility
    {
        /// <summary>
        /// Logs a tracing-level event
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        /// <param name="exception">The exception, if there is one, related to the log message</param>
        [UsedImplicitly]
        public static void Trace(this ILog log, string message, Exception exception)
        {
            log?.Logger?.Log(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType, log4net.Core.Level.Trace, message, exception);
        }

        /// <summary>
        /// Logs a tracing-level event
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        [UsedImplicitly]
        public static void Trace(this ILog log, string message)
        {
            log.Trace(message, null);
        }

        /// <summary>
        /// Logs a tracing-level event using string format parameters
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        /// <param name="args">The arguments to format into the <paramref name="message"/> format string</param>
        [StringFormatMethod("message"), UsedImplicitly]
        public static void TraceFormat(this ILog log, string message, params object[] args)
        {
            if (message == null)
                return;

            if (args == null)
                args = new object[0];

            log.Trace(string.Format(message, args), null);
        }

        /// <summary>
        /// Logs a verbose-level event
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        /// <param name="exception">The exception, if there is one, related to the log message</param>
        [UsedImplicitly]
        public static void Verbose(this ILog log, string message, Exception exception)
        {
            log?.Logger?.Log(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType, log4net.Core.Level.Verbose, message, exception);
        }

        /// <summary>
        /// Logs a verbose-level event
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        [UsedImplicitly]
        public static void Verbose(this ILog log, string message)
        {
            log.Verbose(message, null);
        }

        /// <summary>
        /// Logs a verbose-level event using string format parameters
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        /// <param name="args">The arguments to format into the <paramref name="message"/> format string</param>
        [StringFormatMethod("message"), UsedImplicitly]
        public static void VerboseFormat(this ILog log, string message, params object[] args)
        {
            if (message == null)
                return;

            if (args == null)
                args = new object[0];

            log.Verbose(string.Format(message, args), null);
        }
    }
}
