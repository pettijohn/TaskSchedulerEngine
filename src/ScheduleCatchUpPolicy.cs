/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */

namespace TaskSchedulerEngine
{
    /// <summary>
    /// Determines how the evaluation loop behaves when it falls behind the current UTC second.
    /// </summary>
    public enum ScheduleCatchUpPolicy
    {
        /// <summary>
        /// Evaluate every missed second in order. This preserves the original scheduler behavior.
        /// </summary>
        ReplayAllMissedSeconds,

        /// <summary>
        /// When the configured backlog threshold is exceeded, skip older missed seconds and evaluate
        /// only the most recent whole UTC second.
        /// </summary>
        SkipToLatestSecond
    }
}
