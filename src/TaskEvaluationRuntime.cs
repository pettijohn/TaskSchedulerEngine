/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace TaskSchedulerEngine
{
    /// <summary>
    /// A worker thread wakes up at the start of every second and processes all 
    /// of the <see cref="ScheduleRule"/> records.
    /// This class is thread safe. 
    /// </summary>
    public class TaskEvaluationRuntime
    {
        /// <summary>
        /// Creates a runtime backed by the system UTC clock.
        /// </summary>
        public TaskEvaluationRuntime()
            : this(() => DateTimeOffset.UtcNow)
        {
        }

        /// <summary>
        /// Internal clock-injection constructor used by tests to control the current UTC time.
        /// Production callers use the public constructor above, which supplies DateTimeOffset.UtcNow.
        /// Keeping the clock behind a delegate lets tests advance retry schedules instantly instead
        /// of waiting for real second boundaries, without exposing test-only behavior publicly.
        /// </summary>
        internal TaskEvaluationRuntime(Func<DateTimeOffset> utcNow)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _runningTasks = new ConcurrentDictionary<Task, Task>();
            _schedule = new ConcurrentDictionary<ScheduleRule, ScheduleEvaluationOptimized>();
        }

        // All scheduler-owned reads of the current time should go through this delegate. In
        // particular, retries use UtcNow so their due times follow the same injected test clock.
        private readonly Func<DateTimeOffset> _utcNow;
        internal DateTimeOffset UtcNow => _utcNow();

        /// <summary>
        /// All of the schedules, keyed by ScheduleRule, in a thread-safe Concurrent Dictionary
        /// </summary>
        private ConcurrentDictionary<ScheduleRule, ScheduleEvaluationOptimized> _schedule { get; set; }

        /// <summary>
        /// All currently running tasks. 
        /// </summary>
        private ConcurrentDictionary<Task, Task> _runningTasks;

        /// <summary>
        /// Hang onto the next second to evaluate. Thread-safe.
        /// </summary>
        private DateTimeOffset _nextSecondToEvaluate = DateTimeOffset.Now;
        private object _lock_nextSecondToEvaluate = new object();

        /// <summary>
        /// A flag to determine whether or not the pump is running.
        /// </summary>
        private TaskEvaluationRuntimeState _runState = TaskEvaluationRuntimeState.Stopped;
        private object _lock_runState = new object();
        public bool Stopped
        {
            get
            {
                lock (_lock_runState)
                {
                    return _runState == TaskEvaluationRuntimeState.Stopped;
                }
            }
        }

        public Action<Exception>? UnhandledScheduledTaskException;

        private CancellationTokenSource? _evaluationLoopCancellationToken;
        private TimeSpan _catchUpWarningThreshold = TimeSpan.FromSeconds(60);
        private bool _catchUpWarningActive;

        /// <summary>
        /// Controls how the runtime handles missed seconds after the evaluation loop falls behind.
        /// The default preserves the original behavior and replays every missed second in order.
        /// </summary>
        public ScheduleCatchUpPolicy CatchUpPolicy { get; set; } = ScheduleCatchUpPolicy.ReplayAllMissedSeconds;

        /// <summary>
        /// Backlog duration that causes the runtime to report a catch-up warning.
        /// </summary>
        public TimeSpan CatchUpWarningThreshold
        {
            get
            {
                return _catchUpWarningThreshold;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(value), "Catch-up warning threshold must be greater than zero.");

                _catchUpWarningThreshold = value;
            }
        }

        /// <summary>
        /// Raised when the evaluation loop detects a missed-second backlog at or above
        /// <see cref="CatchUpWarningThreshold"/>.
        /// </summary>
        public event EventHandler<ScheduleCatchUpEventArgs>? ScheduleCatchUpWarning;

        /// <summary>
        /// Raised when an individual schedule rule throws while being evaluated. The failed
        /// schedule is skipped for that evaluated second, but remains active for future seconds.
        /// </summary>
        public event EventHandler<ScheduleRuleEvaluationExceptionEventArgs>? ScheduleRuleEvaluationException;

        /// <summary>
        /// Keep a counter of how many Tasks have executed. Each Task invocation will have a unique sequential ID.
        /// Only update with System.Threading.Interlocked.Increment(). 
        /// </summary>
        private static long TaskID = 0;

        /// <summary>
        /// Start the evaluation pump on a worker thread, waits for the thread to stop, then gracefully shuts down.
        /// Call RequestStop to stop the background thread. 
        /// </summary>
        public async Task RunAsync()
        {
            lock (_lock_runState)
            {
                if (_runState != TaskEvaluationRuntimeState.Stopped)
                    throw new InvalidOperationException("Can only start the service when stopped.");
                _evaluationLoopCancellationToken = new CancellationTokenSource();
                _runState = TaskEvaluationRuntimeState.Running;
            }

            Exception? evaluationLoopException = null;
            try
            {
                //Invoke the worker on its own thread.
                var pumpTask = System.Threading.Tasks.Task.Run(this.EvaluationLoop);
                // Yield control and wait for PumpInteral's thread to end, signaled by RequestStop()
                await pumpTask;
            }
            catch (Exception e)
            {
                evaluationLoopException = e;
            }
            finally
            {
                await StopAsync();
            }

            if (evaluationLoopException != null)
                ExceptionDispatchInfo.Capture(evaluationLoopException).Throw();
        }

        /// <summary>
        /// Request stop, and then wait for it to stop
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            RequestStop();

            lock (_lock_runState)
            {
                _runState = TaskEvaluationRuntimeState.StoppingGracefully;
            }
            Trace.WriteLine($"Waiting for {_runningTasks.Count} Tasks to complete.", "TaskSchedulerEngine");
            await Task.WhenAll(_runningTasks.Keys);
            lock (_lock_runState)
            {
                _runState = TaskEvaluationRuntimeState.Stopped;
            }
            Trace.WriteLine("Stopped", "TaskSchedulerEngine");
        }

        /// <summary>
        /// Sets a flag requesting the background thread to stop. 
        /// Returns immediately - you should already be awaiting on RunAsync. 
        /// </summary>
        /// <returns>True if stop requested successfully; false if stop already in progress or already stopped</returns>
        public bool RequestStop()
        {
            //Request that it stop running.
            lock (_lock_runState)
            {
                if (_runState == TaskEvaluationRuntimeState.Stopped
                    || _runState == TaskEvaluationRuntimeState.StoppingGracefully
                    || _runState == TaskEvaluationRuntimeState.StopRequested)
                    return false;

                _runState = TaskEvaluationRuntimeState.StopRequested;
                if (_evaluationLoopCancellationToken != null)
                    _evaluationLoopCancellationToken.Cancel();
                return true;
            }
        }


        /// <summary>
        /// The actual evaluation loop. Determine the next second that will occur and how long until it occurs.
        /// Sleep that long and evaluate the target evaluation second. Add one second to the target evaluation second,
        /// sleep until that second occurs, and repeat evaluation. 
        /// </summary>
        private async Task EvaluationLoop()
        {
            Trace.WriteLine("Pump Internal on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId, "TaskSchedulerEngine");
            CancellationTokenSource evaluationLoopCancellationToken;
            lock (_lock_runState)
            {
                evaluationLoopCancellationToken = _evaluationLoopCancellationToken
                    ?? throw new InvalidOperationException("The evaluation loop was started without a cancellation token.");
            }
            //The first time through, we need to set up the initial values.

            //Compute the floor of the current second.
            //There are 10,000,000 ticks per second. Subtract the remainder from now.
            DateTimeOffset utcNow = UtcNow;
            DateTimeOffset utcNowFloor = FloorUtcSecond(utcNow);

            //Compute the floor of the next second, and save it as the nextSecondToEvaluate
            lock (_lock_nextSecondToEvaluate)
            {
                _nextSecondToEvaluate = utcNowFloor.AddSeconds(1);
            }

            try
            {
                //Begin the evaluation pump
                while (!evaluationLoopCancellationToken.IsCancellationRequested)
                {
                    lock (_lock_runState)
                    {
                        if (_runState != TaskEvaluationRuntimeState.Running)
                        {
                            break;
                        }
                    }

                    TimeSpan timeUntilNextEvaluation = TimeSpan.Zero;
                    //Determine how long from now it is until the nextSecondToEvaluate occurs.
                    //In the general case, timeUntilNextEvaluation will be less than one second in the future.
                    lock (_lock_nextSecondToEvaluate)
                    {
                        utcNow = UtcNow;
                        timeUntilNextEvaluation = _nextSecondToEvaluate - utcNow;
                    }

                    //If the timeUntilNextEvaluation is positive, sleep that long, otherwise evaluate immediately.
                    //This also serves as a preventative step so that we can catch up if we ever drift.
                    if (timeUntilNextEvaluation > TimeSpan.Zero)
                    {
                        // Await Thread.Delay with cancellation token, and cancel immediately when StopRequest
                        try
                        {
                            await Task.Delay(timeUntilNextEvaluation, evaluationLoopCancellationToken.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }

                    //TODO : use a stopwatch to capture how long the Evaluate method takes and publish a perf counter.
                    // "Percent time spent in evaluation." If it gets close to a second, we're in trouble. 
                    EvaluateNextDueSecond(UtcNow);
                }
            }
            finally
            {
                lock (_lock_runState)
                {
                    evaluationLoopCancellationToken.Dispose();
                    if (ReferenceEquals(_evaluationLoopCancellationToken, evaluationLoopCancellationToken))
                        _evaluationLoopCancellationToken = null;
                }
            }
        }

        /// <summary>
        /// Normalize a clock reading to the exact whole UTC second that has already occurred.
        /// Schedule evaluation is second-granular, so fractional ticks should never influence
        /// whether a rule matches or how far behind the evaluation loop appears to be.
        /// </summary>
        private static DateTimeOffset FloorUtcSecond(DateTimeOffset utcNow)
        {
            DateTimeOffset utc = utcNow.ToUniversalTime();
            return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        }

        /// <summary>
        /// Evaluate one due second according to the active catch-up policy, then advance the cursor.
        /// In the normal case this evaluates exactly _nextSecondToEvaluate. If the runtime has fallen
        /// behind, GetSecondToEvaluate decides whether to replay that oldest missed second or skip ahead.
        /// </summary>
        private int EvaluateNextDueSecond(DateTimeOffset utcNow)
        {
            DateTimeOffset secondToEvaluate;
            DateTimeOffset latestDueSecond = FloorUtcSecond(utcNow);

            lock (_lock_nextSecondToEvaluate)
            {
                // The loop can arrive here with a slightly early clock reading after a short delay.
                // In that case there is nothing due yet, so leave the cursor untouched.
                if (_nextSecondToEvaluate > utcNow)
                    return 0;

                secondToEvaluate = GetSecondToEvaluate(latestDueSecond);
            }

            // Evaluate outside the cursor lock so task dispatch and schedule enumeration do not block
            // callers that need to inspect or update scheduler timing state.
            int matchCount = Evaluate(secondToEvaluate);

            lock (_lock_nextSecondToEvaluate)
            {
                // Advance from the actual evaluated second. This preserves replay behavior by moving
                // one second at a time, and makes skip mode land immediately after the latest due second.
                _nextSecondToEvaluate = secondToEvaluate.AddSeconds(1);
            }

            return matchCount;
        }

        /// <summary>
        /// Choose the next second to evaluate and raise at most one warning for a continuous backlog.
        /// The default policy returns the oldest missed second so existing "replay every second" behavior
        /// is preserved. Skip mode returns latestDueSecond once the configured threshold is exceeded.
        /// </summary>
        private DateTimeOffset GetSecondToEvaluate(DateTimeOffset latestDueSecond)
        {
            DateTimeOffset originalNextSecondToEvaluate = _nextSecondToEvaluate;
            TimeSpan backlog = latestDueSecond - originalNextSecondToEvaluate;
            bool thresholdExceeded = backlog >= CatchUpWarningThreshold;
            bool skipApplied = thresholdExceeded && CatchUpPolicy == ScheduleCatchUpPolicy.SkipToLatestSecond;

            if (thresholdExceeded)
            {
                if (!_catchUpWarningActive)
                {
                    // In skip mode, backlog.TotalSeconds is the count of old seconds being skipped:
                    // originalNextSecondToEvaluate through the second before latestDueSecond.
                    long skippedSeconds = skipApplied ? Convert.ToInt64(backlog.TotalSeconds) : 0;
                    ReportCatchUpWarning(
                        originalNextSecondToEvaluate,
                        latestDueSecond,
                        backlog,
                        skippedSeconds,
                        skipApplied);
                    _catchUpWarningActive = true;
                }

                if (skipApplied)
                    return latestDueSecond;
            }
            else
            {
                // Reset once the runtime has caught up enough that a later backlog episode is reported.
                _catchUpWarningActive = false;
            }

            return originalNextSecondToEvaluate;
        }

        /// <summary>
        /// Publish catch-up diagnostics without taking a dependency on a logging abstraction.
        /// Consumers can subscribe to ScheduleCatchUpWarning and bridge it to their logger of choice;
        /// Trace keeps the existing lightweight diagnostics path for this dependency-free library.
        /// </summary>
        private void ReportCatchUpWarning(
            DateTimeOffset originalNextSecondToEvaluate,
            DateTimeOffset latestDueSecond,
            TimeSpan backlog,
            long skippedSeconds,
            bool skipApplied)
        {
            var eventArgs = new ScheduleCatchUpEventArgs(
                UtcNow,
                originalNextSecondToEvaluate,
                latestDueSecond,
                backlog,
                CatchUpPolicy,
                skippedSeconds,
                skipApplied);

            Trace.WriteLine(
                $"Schedule catch-up backlog detected. BacklogSeconds={backlog.TotalSeconds}, Policy={CatchUpPolicy}, SkipApplied={skipApplied}, SkippedSeconds={skippedSeconds}, OriginalNextSecondUtc={originalNextSecondToEvaluate:o}, LatestDueSecondUtc={latestDueSecond:o}",
                "TaskSchedulerEngine");

            try
            {
                ScheduleCatchUpWarning?.Invoke(this, eventArgs);
            }
            catch (Exception e)
            {
                // Diagnostic event handlers are user code. Treat failures the same way as scheduled
                // task failures: report them, but never let them terminate the evaluation loop.
                Trace.WriteLine("Unhandled exception in ScheduleCatchUpWarning handler: " + e, "TaskSchedulerEngine");
                if (UnhandledScheduledTaskException != null)
                    UnhandledScheduledTaskException(e);
            }
        }

        /// <summary>
        /// Evaluate all of the rules in the schedule and see if they match the specified second. If it matches, spin it off on a new thread. 
        /// </summary>
        internal int Evaluate(DateTimeOffset secondToEvaluate)
        {
            //TODO : convert secondToEvaluate to a faster format and avoid the extra bit-shifts downstream.
            int i = 0;

            foreach (KeyValuePair<ScheduleRule, ScheduleEvaluationOptimized> scheduleItem in _schedule)
            {
                //Check for & delete expired schedules 
                if (secondToEvaluate > scheduleItem.Key.Expiration)
                {
                    // Is this safe? 
                    _schedule.Remove(scheduleItem.Key, out _);
                    continue;
                }

                bool match;
                try
                {
                    match = scheduleItem.Value.EvaluateRuleMatch(secondToEvaluate);
                }
                catch (Exception e)
                {
                    ReportScheduleRuleEvaluationException(scheduleItem.Key, secondToEvaluate, e);
                    continue;
                }

                if (match)
                {
                    var eventArgs = new ScheduleRuleMatchEventArgs(
                        UtcNow,
                        secondToEvaluate,
                        Interlocked.Increment(ref TaskEvaluationRuntime.TaskID),
                        scheduleItem.Key,
                        this
                    );

                    i++;
                    Task? workerTask = null;
                    try
                    {
                        var ct = _evaluationLoopCancellationToken?.Token ?? CancellationToken.None;
                        // run in it's own task so that any sync code doesn't block the scheduler.
                        workerTask = Task.Run<bool>(() => scheduleItem.Value.Task.OnScheduleRuleMatch(eventArgs, ct), ct);

                        // Keep a ConcurrentDict of running Tasks for graceful shutdown 
                        _runningTasks[workerTask] = workerTask;
                        Trace.WriteLine("Running task count: " + _runningTasks.Count, "TaskSchedulerEngine");
                        workerTask.ContinueWith((t) =>
                        {
                            // Remove myself from the running tasks
                            _runningTasks.Remove(t, out _);

                            // Check for exception and pass to handler
                            if (t.IsFaulted)
                                if (UnhandledScheduledTaskException != null && t.Exception != null)
                                    UnhandledScheduledTaskException(t.Exception);
                        });
                    }
                    catch (Exception e)
                    {
                        // This code block should never be reached - the ContinueWith handler 
                        // should take care of it. But I don't want the main loop to ever die, 
                        // so being extra cautious. 
                        if (UnhandledScheduledTaskException != null)
                            UnhandledScheduledTaskException(e);
                    }
                }
            }

            return i;
        }

        private void ReportScheduleRuleEvaluationException(
            ScheduleRule scheduleRule,
            DateTimeOffset timeEvaluatedUtc,
            Exception exception)
        {
            var eventArgs = new ScheduleRuleEvaluationExceptionEventArgs(
                scheduleRule,
                timeEvaluatedUtc,
                exception);

            Trace.WriteLine(
                $"Schedule rule evaluation failed. ScheduleName={scheduleRule.Name}, TimeEvaluatedUtc={timeEvaluatedUtc:o}, Exception={exception}",
                "TaskSchedulerEngine");

            try
            {
                ScheduleRuleEvaluationException?.Invoke(this, eventArgs);
            }
            catch (Exception e)
            {
                // Diagnostic event handlers are user code. Report failures, but keep the
                // scheduler loop alive and continue evaluating other rules.
                Trace.WriteLine("Unhandled exception in ScheduleRuleEvaluationException handler: " + e, "TaskSchedulerEngine");
                if (UnhandledScheduledTaskException != null)
                    UnhandledScheduledTaskException(e);
            }
        }

        /// <summary>
        /// Evaluate one instant and await callbacks dispatched by that evaluation.
        /// Intended for deterministic unit testing without running the clock-driven loop.
        /// </summary>
        internal async Task<int> EvaluateAndWaitAsync(DateTimeOffset secondToEvaluate)
        {
            int matchCount = Evaluate(secondToEvaluate);
            var tasks = new List<Task>(_runningTasks.Keys);
            await Task.WhenAll(tasks);
            return matchCount;
        }

        internal void SetNextSecondToEvaluate(DateTimeOffset nextSecondToEvaluate)
        {
            lock (_lock_nextSecondToEvaluate)
            {
                _nextSecondToEvaluate = nextSecondToEvaluate;
            }
        }

        internal async Task<int> EvaluateNextDueSecondAndWaitAsync(DateTimeOffset utcNow)
        {
            int matchCount = EvaluateNextDueSecond(utcNow);
            var tasks = new List<Task>(_runningTasks.Keys);
            await Task.WhenAll(tasks);
            return matchCount;
        }

        /// <summary>
        /// Create a new <see cref="ScheduleRule">ScheduleRule and 
        /// link it to this runtime.
        /// </summary>
        public ScheduleRule CreateSchedule()
        {
            return new ScheduleRule(this);
        }

        /// <summary>
        /// Unconditionally update (or add) the schedule with a matching name.
        /// </summary>
        internal void UpdateSchedule(ScheduleRule sched)
        {
            // Once a task is attached, we will treat it as valid and begin evaluating it
            if (sched != null && sched.Task != null)
            {
                if (!sched.Active)
                {
                    var deleted = false;
                    deleted = _schedule.TryRemove(sched, out _);
                }
                else
                {
                    ScheduleEvaluationOptimized schedule = new ScheduleEvaluationOptimized(sched);
                    _schedule[sched] = schedule;
                }
            }
        }

    }
}
