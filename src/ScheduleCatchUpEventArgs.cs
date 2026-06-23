/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// Diagnostic information reported when the evaluation loop detects a missed-second backlog.
    /// </summary>
    public class ScheduleCatchUpEventArgs : EventArgs
    {
        public ScheduleCatchUpEventArgs(
            DateTimeOffset detectedAtUtc,
            DateTimeOffset originalNextSecondToEvaluateUtc,
            DateTimeOffset latestDueSecondUtc,
            TimeSpan backlog,
            ScheduleCatchUpPolicy policy,
            long skippedSeconds,
            bool skipApplied)
        {
            DetectedAtUtc = detectedAtUtc;
            OriginalNextSecondToEvaluateUtc = originalNextSecondToEvaluateUtc;
            LatestDueSecondUtc = latestDueSecondUtc;
            Backlog = backlog;
            Policy = policy;
            SkippedSeconds = skippedSeconds;
            SkipApplied = skipApplied;
        }

        /// <summary>
        /// UTC instant when the backlog was detected.
        /// </summary>
        public DateTimeOffset DetectedAtUtc { get; private set; }

        /// <summary>
        /// The oldest missed second the runtime was about to evaluate before applying any catch-up policy.
        /// </summary>
        public DateTimeOffset OriginalNextSecondToEvaluateUtc { get; private set; }

        /// <summary>
        /// The most recent whole UTC second that is due for evaluation.
        /// </summary>
        public DateTimeOffset LatestDueSecondUtc { get; private set; }

        /// <summary>
        /// How far behind the evaluation loop was when the warning was raised.
        /// </summary>
        public TimeSpan Backlog { get; private set; }

        /// <summary>
        /// The policy that was active when the backlog was detected.
        /// </summary>
        public ScheduleCatchUpPolicy Policy { get; private set; }

        /// <summary>
        /// Number of older missed seconds skipped by the runtime.
        /// </summary>
        public long SkippedSeconds { get; private set; }

        /// <summary>
        /// True when the runtime skipped older missed seconds instead of replaying the full backlog.
        /// </summary>
        public bool SkipApplied { get; private set; }
    }
}
