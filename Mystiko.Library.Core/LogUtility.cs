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
    using System.Threading;

    using JetBrains.Annotations;

    using log4net;
    using log4net.Core;

    /// <summary>
    /// A set of utility methods for using additional functionality of the logging mechanism.
    /// </summary>
    internal static class LogUtility
    {
        /// <summary>
        /// Logs a tracing-level event
        /// </summary>
        /// <param name="log">
        /// The logger implementation to which to log the <paramref name="message"/>
        /// </param>
        /// <param name="message">
        /// The message to submit to the logger
        /// </param>
        /// <param name="exception">
        /// The exception, if there is one, related to the log message
        /// </param>
        /// <param name="callerFilePath">File path of the caller</param>
        /// <param name="callerLineNumber">Line number of the caller</param>
        /// <param name="callerMemberName">Member name of the caller</param>
        [UsedImplicitly]
        public static void Trace(
            [NotNull] this ILog log, 
            [NotNull] string message, 
            [CanBeNull] Exception exception = null,
            [CanBeNull, System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0,
            [CanBeNull, System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null)
        {
            log.Logger?.Log(new LoggingEvent(
                    new LoggingEventData
                    {
                        LocationInfo =
                                new LocationInfo(
                                    null,
                                    callerMemberName,
                                    callerFilePath,
                                    callerLineNumber.ToString()),
                        ExceptionString = exception?.ToString(),
                        Level = Level.Trace,
                        Message = message,
                        ThreadName = Thread.CurrentThread?.Name,
                        TimeStampUtc = DateTime.UtcNow
                    }));
        }

        /// <summary>
        /// Logs a verbose-level event
        /// </summary>
        /// <param name="log">The logger implementation to which to log the <paramref name="message"/></param>
        /// <param name="message">The message to submit to the logger</param>
        /// <param name="exception">The exception, if there is one, related to the log message</param>
        /// <param name="callerFilePath">File path of the caller</param>
        /// <param name="callerLineNumber">Line number of the caller</param>
        /// <param name="callerMemberName">Member name of the caller</param>
        [UsedImplicitly]
        public static void Verbose(
            [NotNull] this ILog log,
            [NotNull] string message, 
            [CanBeNull] Exception exception = null,
            [CanBeNull, System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0,
            [CanBeNull, System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null)
        {
            log.Logger?.Log(new LoggingEvent(
                    new LoggingEventData
                        {
                            LocationInfo =
                                new LocationInfo(
                                    null,
                                    callerMemberName,
                                    callerFilePath,
                                    callerLineNumber.ToString()),
                            ExceptionString = exception?.ToString(),
                            Level = Level.Verbose,
                            Message = message,
                            ThreadName = Thread.CurrentThread?.Name,
                            TimeStampUtc = DateTime.UtcNow
                        }));
        }
    }
}
