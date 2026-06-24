using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaskSchedulerEngine;
using static SchedulerEngineRuntimeTests.TestAssert;

namespace SchedulerEngineRuntimeTests
{
    [TestClass]
    public class TaskEvaluationRuntimeTest
    {
        [TestMethod]
        public async Task ExecuteTest()
        {
            bool executed = false;
            var runtime = new TaskEvaluationRuntime();
            runtime.CreateSchedule()
                .Execute((e, token) =>
                {
                    executed = true;
                    return true;
                });

            int matches = await runtime.EvaluateAndWaitAsync(CurrentTestTime());

            Assert.AreEqual(1, matches);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task ExecuteAsyncTest()
        {
            bool executed = false;
            var runtime = new TaskEvaluationRuntime();
            runtime.CreateSchedule()
                .Execute(async (e, token) =>
                {
                    await Task.Yield();
                    executed = true;
                    return true;
                });

            int matches = await runtime.EvaluateAndWaitAsync(CurrentTestTime());

            Assert.AreEqual(1, matches);
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public async Task ScheduledTasksExecuteConcurrently()
        {
            int activeCallbacks = 0;
            int maximumConcurrency = 0;
            int enteredCallbacks = 0;
            var bothEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseCallbacks = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime();

            Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback = async (e, token) =>
            {
                int active = Interlocked.Increment(ref activeCallbacks);
                UpdateMaximum(ref maximumConcurrency, active);
                if (Interlocked.Increment(ref enteredCallbacks) == 2)
                    bothEntered.TrySetResult(true);

                await releaseCallbacks.Task;
                Interlocked.Decrement(ref activeCallbacks);
                return true;
            };

            runtime.CreateSchedule().Execute(callback);
            runtime.CreateSchedule().Execute(callback);

            Task<int> evaluation = runtime.EvaluateAndWaitAsync(CurrentTestTime());
            await AwaitWithTimeout(bothEntered.Task);
            releaseCallbacks.SetResult(true);

            Assert.AreEqual(2, await evaluation);
            Assert.AreEqual(2, maximumConcurrency);
        }

        [TestMethod]
        public async Task RemoveExpired()
        {
            bool executed = false;
            DateTimeOffset evaluationTime = CurrentTestTime();
            var runtime = new TaskEvaluationRuntime();
            runtime.CreateSchedule()
                .ExpiresAfter(evaluationTime.AddSeconds(-1))
                .Execute((e, token) =>
                {
                    executed = true;
                    return true;
                });

            int matches = await runtime.EvaluateAndWaitAsync(evaluationTime);

            Assert.AreEqual(0, matches);
            Assert.IsFalse(executed);
        }

        [TestMethod]
        public async Task UnhandledTaskExceptionIsReported()
        {
            var reportedException = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime
            {
                UnhandledScheduledTaskException = e => reportedException.TrySetResult(e)
            };
            runtime.CreateSchedule()
                .Execute((Func<ScheduleRuleMatchEventArgs, CancellationToken, bool>)
                    ((e, token) => throw new InvalidOperationException("Expected test failure")));

            runtime.Evaluate(CurrentTestTime());
            Exception exception = await AwaitWithTimeout(reportedException.Task);

            Assert.Contains("Expected test failure", exception.ToString());
        }

        [TestMethod]
        public async Task RunAsyncRejectsSecondStartAndReturnsToStopped()
        {
            var runtime = new TaskEvaluationRuntime();

            Task run = runtime.RunAsync();
            await ThrowsAsync<InvalidOperationException>(() => runtime.RunAsync());
            Assert.IsTrue(runtime.RequestStop());
            await run;

            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public async Task RequestStopReflectsLifecycleState()
        {
            var runtime = new TaskEvaluationRuntime();

            Assert.IsTrue(runtime.Stopped);
            Assert.IsFalse(runtime.RequestStop());

            Task run = runtime.RunAsync();
            Assert.IsTrue(runtime.RequestStop());
            Assert.IsFalse(runtime.RequestStop());
            await run;

            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public async Task RunAsyncStopsWhenCancellationTokenIsCanceled()
        {
            var runtime = new TaskEvaluationRuntime();
            using (var cts = new CancellationTokenSource())
            {
                Task run = runtime.RunAsync(cts.Token);

                cts.Cancel();
                await AwaitWithTimeout(run);
            }

            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public async Task RuntimeCanRestartAfterStopping()
        {
            var runtime = new TaskEvaluationRuntime();

            Task firstRun = runtime.RunAsync();
            Assert.IsTrue(runtime.RequestStop());
            await firstRun;

            Task secondRun = runtime.RunAsync();
            Assert.IsTrue(runtime.RequestStop());
            await secondRun;

            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public async Task RunAsyncDrainTimesOutAndReturnsToStoppedWhenCallbackDoesNotComplete()
        {
            var callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime
            {
                ShutdownTimeout = TimeSpan.FromMilliseconds(20)
            };
            runtime.CreateSchedule()
                .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(1))
                .Execute(async (e, token) =>
                {
                    callbackStarted.SetResult(true);
                    await releaseCallback.Task;
                    return true;
                });

            Task run = runtime.RunAsync();
            await AwaitWithTimeout(callbackStarted.Task);
            Assert.IsTrue(runtime.RequestStop());
            await AwaitWithTimeout(run);

            Assert.IsTrue(runtime.Stopped);

            releaseCallback.SetResult(true);
        }

        [TestMethod]
        public async Task RunAsyncDoesNotFaultWhenCallbackFaulted()
        {
            var callbackReported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime
            {
                ShutdownTimeout = TimeSpan.FromSeconds(1),
                UnhandledScheduledTaskException = e => callbackReported.TrySetResult(e)
            };
            runtime.CreateSchedule()
                .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(1))
                .Execute((Func<ScheduleRuleMatchEventArgs, CancellationToken, bool>)
                    ((e, token) =>
                    {
                        e.Runtime.RequestStop();
                        throw new InvalidOperationException("Expected shutdown fault test failure.");
                    }));

            Task run = runtime.RunAsync();
            await AwaitWithTimeout(callbackReported.Task);
            await AwaitWithTimeout(run);

            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public async Task CallbackCanRequestStop()
        {
            var requestStopResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime();
            runtime.CreateSchedule()
                .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(1))
                .Execute((e, token) =>
                {
                    requestStopResult.SetResult(e.Runtime.RequestStop());
                    return true;
                });

            Task run = runtime.RunAsync();
            bool requested = await AwaitWithTimeout(requestStopResult.Task);
            await AwaitWithTimeout(run);

            Assert.IsTrue(requested);
            Assert.IsTrue(runtime.Stopped);
        }

        [TestMethod]
        public void ShutdownTimeoutRejectsInvalidValues()
        {
            var runtime = new TaskEvaluationRuntime();

            Throws<ArgumentOutOfRangeException>(() => runtime.ShutdownTimeout = TimeSpan.Zero);
            Throws<ArgumentOutOfRangeException>(() => runtime.ShutdownTimeout = TimeSpan.FromSeconds(-1));
            Throws<ArgumentOutOfRangeException>(() => runtime.ShutdownTimeout = TimeSpan.MaxValue);

            runtime.ShutdownTimeout = Timeout.InfiniteTimeSpan;
            Assert.AreEqual(Timeout.InfiniteTimeSpan, runtime.ShutdownTimeout);
        }

        [TestMethod]
        public async Task CatchUpDefaultPolicyWarnsAndReplaysOldestMissedSecond()
        {
            DateTimeOffset firstMissedSecond = CurrentTestTime();
            DateTimeOffset utcNow = firstMissedSecond.AddSeconds(120);
            var invoked = new ConcurrentQueue<DateTimeOffset>();
            var warnings = new ConcurrentQueue<ScheduleCatchUpEventArgs>();
            var runtime = new TaskEvaluationRuntime(() => utcNow)
            {
                CatchUpWarningThreshold = TimeSpan.FromSeconds(60)
            };
            runtime.ScheduleCatchUpWarning += (sender, e) => warnings.Enqueue(e);
            runtime.SetNextSecondToEvaluate(firstMissedSecond);
            runtime.CreateSchedule()
                .Execute((e, token) =>
                {
                    invoked.Enqueue(e.TimeScheduledUtc);
                    return true;
                });

            int matches = await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow);

            Assert.AreEqual(1, matches);
            Assert.IsTrue(warnings.TryPeek(out ScheduleCatchUpEventArgs warning));
            Assert.AreEqual(ScheduleCatchUpPolicy.ReplayAllMissedSeconds, warning.Policy);
            Assert.AreEqual(TimeSpan.FromSeconds(120), warning.Backlog);
            Assert.AreEqual(0, warning.SkippedSeconds);
            Assert.IsFalse(warning.SkipApplied);
            CollectionAssert.AreEqual(new[] { firstMissedSecond }, invoked.ToArray());
        }

        [TestMethod]
        public async Task CatchUpWarningDoesNotSpamDuringSameBacklogEpisode()
        {
            DateTimeOffset firstMissedSecond = CurrentTestTime();
            DateTimeOffset utcNow = firstMissedSecond.AddSeconds(120);
            int warnings = 0;
            var runtime = new TaskEvaluationRuntime(() => utcNow)
            {
                CatchUpWarningThreshold = TimeSpan.FromSeconds(60)
            };
            runtime.ScheduleCatchUpWarning += (sender, e) => warnings++;
            runtime.SetNextSecondToEvaluate(firstMissedSecond);
            runtime.CreateSchedule().Execute((e, token) => true);

            Assert.AreEqual(1, await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow));
            Assert.AreEqual(1, await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow));

            Assert.AreEqual(1, warnings);
        }

        [TestMethod]
        public async Task CatchUpSkipPolicyEvaluatesLatestDueSecond()
        {
            DateTimeOffset firstMissedSecond = CurrentTestTime();
            DateTimeOffset utcNow = firstMissedSecond.AddSeconds(120);
            var invoked = new ConcurrentQueue<DateTimeOffset>();
            var warnings = new ConcurrentQueue<ScheduleCatchUpEventArgs>();
            var runtime = new TaskEvaluationRuntime(() => utcNow)
            {
                CatchUpPolicy = ScheduleCatchUpPolicy.SkipToLatestSecond,
                CatchUpWarningThreshold = TimeSpan.FromSeconds(60)
            };
            runtime.ScheduleCatchUpWarning += (sender, e) => warnings.Enqueue(e);
            runtime.SetNextSecondToEvaluate(firstMissedSecond);
            runtime.CreateSchedule()
                .Execute((e, token) =>
                {
                    invoked.Enqueue(e.TimeScheduledUtc);
                    return true;
                });

            Assert.AreEqual(1, await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow));
            Assert.AreEqual(0, await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow));

            Assert.IsTrue(warnings.TryPeek(out ScheduleCatchUpEventArgs warning));
            Assert.AreEqual(ScheduleCatchUpPolicy.SkipToLatestSecond, warning.Policy);
            Assert.AreEqual(TimeSpan.FromSeconds(120), warning.Backlog);
            Assert.AreEqual(120, warning.SkippedSeconds);
            Assert.IsTrue(warning.SkipApplied);
            CollectionAssert.AreEqual(new[] { utcNow }, invoked.ToArray());
        }

        [TestMethod]
        public async Task CatchUpDoesNotWarnBelowThreshold()
        {
            DateTimeOffset firstMissedSecond = CurrentTestTime();
            DateTimeOffset utcNow = firstMissedSecond.AddSeconds(59);
            int warnings = 0;
            var runtime = new TaskEvaluationRuntime(() => utcNow)
            {
                CatchUpWarningThreshold = TimeSpan.FromSeconds(60)
            };
            runtime.ScheduleCatchUpWarning += (sender, e) => warnings++;
            runtime.SetNextSecondToEvaluate(firstMissedSecond);
            runtime.CreateSchedule().Execute((e, token) => true);

            Assert.AreEqual(1, await runtime.EvaluateNextDueSecondAndWaitAsync(utcNow));

            Assert.AreEqual(0, warnings);
        }

        [TestMethod]
        public void CatchUpWarningThresholdRejectsZeroOrNegativeValues()
        {
            var runtime = new TaskEvaluationRuntime();

            Throws<ArgumentOutOfRangeException>(() => runtime.CatchUpWarningThreshold = TimeSpan.Zero);
            Throws<ArgumentOutOfRangeException>(() => runtime.CatchUpWarningThreshold = TimeSpan.FromSeconds(-1));
        }

        [TestMethod]
        public async Task ScheduleRuleEvaluationExceptionIsReportedAndOtherSchedulesContinue()
        {
            DateTimeOffset evaluationTime = CurrentTestTime();
            bool invalidScheduleExecuted = false;
            bool validScheduleExecuted = false;
            var reported = new ConcurrentQueue<ScheduleRuleEvaluationExceptionEventArgs>();
            var runtime = new TaskEvaluationRuntime();
            runtime.ScheduleRuleEvaluationException += (sender, e) => reported.Enqueue(e);

            // Public APIs now reject null time zones. Poison the backing field directly so this test
            // still proves the runtime is protected from future/internal corrupted schedule snapshots.
            ScheduleRule invalidSchedule = runtime.CreateSchedule()
                .WithName("Invalid")
                .Execute((e, token) =>
                {
                    invalidScheduleExecuted = true;
                    return true;
                });
            PoisonScheduleTimeZone(invalidSchedule);
            runtime.UpdateSchedule(invalidSchedule);

            runtime.CreateSchedule()
                .WithName("Valid")
                .Execute((e, token) =>
                {
                    validScheduleExecuted = true;
                    return true;
                });

            int matches = await runtime.EvaluateAndWaitAsync(evaluationTime);

            // The poisoned rule should be skipped only for this evaluated second. The valid rule must
            // still execute, proving one bad schedule cannot kill the whole evaluation pass.
            Assert.AreEqual(1, matches);
            Assert.IsFalse(invalidScheduleExecuted);
            Assert.IsTrue(validScheduleExecuted);
            Assert.IsTrue(reported.TryPeek(out ScheduleRuleEvaluationExceptionEventArgs exceptionEvent));
            Assert.AreSame(invalidSchedule, exceptionEvent.ScheduleRule);
            Assert.AreEqual(evaluationTime, exceptionEvent.TimeEvaluatedUtc);
            Assert.IsInstanceOfType(exceptionEvent.Exception, typeof(ArgumentNullException));

            invalidScheduleExecuted = false;
            validScheduleExecuted = false;
            invalidSchedule.WithUtc();

            // The failed schedule is not evicted forever. After fixing it, it participates normally
            // in later evaluations.
            matches = await runtime.EvaluateAndWaitAsync(evaluationTime.AddSeconds(1));

            Assert.AreEqual(2, matches);
            Assert.IsTrue(invalidScheduleExecuted);
            Assert.IsTrue(validScheduleExecuted);
        }

        [TestMethod]
        public async Task ScheduleRuleEvaluationExceptionHandlerFailureIsReportedAndDoesNotStopEvaluation()
        {
            DateTimeOffset evaluationTime = CurrentTestTime();
            bool validScheduleExecuted = false;
            var reportedHandlerException = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
            var runtime = new TaskEvaluationRuntime
            {
                UnhandledScheduledTaskException = e => reportedHandlerException.TrySetResult(e)
            };
            runtime.ScheduleRuleEvaluationException += (sender, e) =>
            {
                // Diagnostic subscribers are user code. A bad handler should be reported, not allowed
                // to become a new way to terminate schedule evaluation.
                throw new InvalidOperationException("Expected diagnostic handler failure.");
            };

            // Same deliberate corruption as above: bypass public validation to simulate a bad snapshot.
            ScheduleRule invalidSchedule = runtime.CreateSchedule()
                .WithName("Invalid")
                .Execute((e, token) => true);
            PoisonScheduleTimeZone(invalidSchedule);
            runtime.UpdateSchedule(invalidSchedule);

            runtime.CreateSchedule()
                .WithName("Valid")
                .Execute((e, token) =>
                {
                    validScheduleExecuted = true;
                    return true;
                });

            int matches = await runtime.EvaluateAndWaitAsync(evaluationTime);
            Exception handlerException = await AwaitWithTimeout(reportedHandlerException.Task);

            // The valid schedule still runs even though evaluating another schedule failed and the
            // diagnostic handler itself threw.
            Assert.AreEqual(1, matches);
            Assert.IsTrue(validScheduleExecuted);
            Assert.Contains("Expected diagnostic handler failure.", handlerException.ToString());
        }

        [TestMethod]
        public async Task ExponentialBackoffTaskTest()
        {
            await AssertRetrySchedule(async (e, token) =>
            {
                await Task.Yield();
                return false;
            });
        }

        [TestMethod]
        public async Task ExponentialBackoffSynchronousTaskTest()
        {
            await AssertRetrySchedule((e, token) => Task.FromResult(false));
        }

        private static async Task AssertRetrySchedule(
            Func<ScheduleRuleMatchEventArgs, CancellationToken, Task<bool>> callback)
        {
            DateTimeOffset now = CurrentTestTime();
            DateTimeOffset firstAttempt = now;
            var invoked = new ConcurrentQueue<DateTimeOffset>();
            var runtime = new TaskEvaluationRuntime(() => now);
            runtime.CreateSchedule()
                .ExecuteOnceAt(firstAttempt)
                .ExecuteAndRetry(async (e, token) =>
                {
                    invoked.Enqueue(e.TimeScheduledUtc);
                    return await callback(e, token);
                }, maxAttempts: 4, baseRetryIntervalSeconds: 2);

            Assert.AreEqual(1, await runtime.EvaluateAndWaitAsync(now));
            now = firstAttempt.AddSeconds(2);
            Assert.AreEqual(1, await runtime.EvaluateAndWaitAsync(now));
            now = firstAttempt.AddSeconds(6);
            Assert.AreEqual(1, await runtime.EvaluateAndWaitAsync(now));
            now = firstAttempt.AddSeconds(14);
            Assert.AreEqual(1, await runtime.EvaluateAndWaitAsync(now));
            now = firstAttempt.AddSeconds(30);
            Assert.AreEqual(0, await runtime.EvaluateAndWaitAsync(now));

            CollectionAssert.AreEqual(
                new[] { 0d, 2d, 6d, 14d },
                invoked.Select(value => (value - firstAttempt).TotalSeconds).ToArray());
        }

        private static DateTimeOffset CurrentTestTime()
        {
            return new DateTimeOffset(ScheduleRule.MinYear + 1, 6, 15, 12, 0, 0, TimeSpan.Zero);
        }

        private static async Task AwaitWithTimeout(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(task, completed, "Timed out waiting for asynchronous test work.");
            await task;
        }

        private static async Task<T> AwaitWithTimeout<T>(Task<T> task)
        {
            await AwaitWithTimeout((Task)task);
            return await task;
        }

        private static void PoisonScheduleTimeZone(ScheduleRule scheduleRule)
        {
            // Test-only corruption helper. Reflection keeps production validation strict while letting
            // us exercise the runtime's defensive exception boundary.
            FieldInfo timeZoneField = typeof(ScheduleRule).GetField(
                "<TimeZone>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(timeZoneField, "Unable to find TimeZone backing field for test setup.");
            timeZoneField.SetValue(scheduleRule, null);
        }
        private static void UpdateMaximum(ref int maximum, int candidate)
        {
            int observed;
            do
            {
                observed = maximum;
                if (candidate <= observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref maximum, candidate, observed) != observed);
        }
    }
}
