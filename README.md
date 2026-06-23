# TaskSchedulerEngine

A lightweight (zero dependencies, core logic is <400 lines of code) cron-like scheduler for in-memory scheduling of your code with second-level precision.
Implement IScheduledTask or provide a callback, define a ScheduleRule, and Start the runtime.
Schedule Rule evaluation is itself lightweight with bitwise evaluation of "now" against the rules (see ScheduleRuleEvaluationOptimized).
Each invoked ScheduledTask runs on its own thread so long running tasks won't block other tasks.
Targets .NET 8, .NET 9, and .NET 10.

## Quick Start

See `sample/` in source tree for more detailed examples.

`dotnet add package TaskSchedulerEngine`

Nuget link: https://www.nuget.org/packages/TaskSchedulerEngine/

Version number scheme is (two digit year).(day of year).(minute of day).

```C#
static async Task Main(string[] args)
{
  // Instantiate TaskEvaluationRuntime.
  var runtime = new TaskEvaluationRuntime();

  // Use the fluent API to define a schedule rule, or set the corresponding properties
  // Execute() accepts an IScheduledTask or Action<ScheduleRuleMatchEventArgs, CancellationToken, bool>
  var s1 = runtime.CreateSchedule()
    .AtSeconds(0)
    .AtMinutes(0, 10, 20, 30, 40, 50)
    // .AtMonths(), .AtDays(), AtDaysOfWeek() ... etc
    // Important note that unset is always *, so if you omit AtSeconds(0) it will execute every second
    .WithName("EveryTenMinutes") // Optional ID for your reference
    .WithTimeZone(TimeZoneInfo.Utc) // Or string such as "America/Los_Angeles"
    .Execute(async (e, token) => {
      if(!token.IsCancellationRequested)
        Console.WriteLine($"{e.TaskId}: Event intended for {e.TimeScheduledUtc:o} occurred at {e.TimeSignaledUtc:o}");
        return true; // Return success. Used by retry scenarios.
    });

  var s2 = runtime.CreateSchedule()
    .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(5))
    .Execute(async (_, _) => { Console.WriteLine("Use ExecuteOnceAt to run this task in 5 seconds. Useful for retry scenarios."); return true; });

  var s3 = runtime.CreateSchedule()
    .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(1))
    .ExecuteAndRetry(
      async (e, _) => {
          // Do something that may fail like a network call - catch & gracefully fail by returning false.
          // Exponential backoff task will retry up to MaxAttempts times.
          return false;
      },
      4, // MaxAttempts, inclusive of initial attempt
      2  // BaseRetryIntervalSeconds
         // Retry delay logic: baseRetrySeconds * (2^retryCount)
         // In this case will retry after 2, 4, 8 second waits
    );

  // You can also create rules from cron expressions; * or comma separated lists are supported
  // (/ and - are NOT supported).
  // Format: minute (0..59), hour (0..23), dayOfMonth (1..31), month (1..12), dayOfWeek (0=Sunday..6).
  // Seconds will always be zero.
  var s4 = runtime.CreateSchedule()
    .FromCron("0,20,40 * * * *")
    .WithName("Every20Sec") //Optional ID for your reference
    .Execute(async (e, token) => {
      if(!token.IsCancellationRequested)
        Console.WriteLine($"Load me from config and change me without recompiling!");
        return true;
    });

  // Finally, there are helper methods ExecuteEvery*() that execute a task at a given interval.
  var s4 = runtime.CreateSchedule()
    // Run the task a 0 minutes and 0 seconds past the hours 0, 6, 12, and 18
    .ExecuteEveryHour(0, 6, 12, 18)
    .Execute(async (e, token) => {
        return true;
    });

  using var cts = new CancellationTokenSource();

  // Handle the shutdown event (CTRL+C, SIGHUP) if graceful shutdown desired.
  AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();

  // Await the runtime. Cancel cts or call RequestStop() to stop.
  await runtime.RunAsync(cts.Token);
}
```

## Terminology

* Schedule Rule - cron-like rule, with second-level precision. Leave a parameter unset/null to treat it as "*", otherwise set an int array for when you want to execute. See usage note above in `EveryTenMinutes` example.
* Scheduled Task - the thing to execute when schedule matches. The instance is shared by all executions forever and should be thread safe (unless you're completely sure there will only ever be at most one invocation). If you need an instance per execution, make ScheduledTask.OnScheduleRuleMatch a factory pattern.
* Schedule Rule Match - the current second ("Now") matches a Schedule Rule so the Scheduled Task should execute. A single ScheduleRule can only execute one ScheduledTask. If you need to execute multiple tasks sequentially, initiate them from your Task. Multiple Schedule Rules that fire at the same time will execute in parallel (order not guaranteed).
* Task Evaluation Runtime - the thing that evaluates the rules each second. Evaluation runs on its own thread and spawns Tasks on their own threads.

## Troubleshooting

* *My task is executing every second, but I scheduled it to run with a different interval.* - You probably need to add .AtSeconds(0). Unspecified/unset/null is always treated as */every. See example above, `EveryTenMinutes`. While there are other ways to solve this problem, this encourages verbosity. Like Cron, consider being verbose and setting every parameter every time.

## Runtime Lifecycle

* Instantiate TaskEvaluationRuntime and await RunAsync() for the full runtime lifetime.
* TaskEvaluationRuntime moves through four states:
  * Stopped: nothing happening, can Start back into a running state.
  * Running: evaluating every second
  * StopRequested: instructs the every-second evaluation loop to quit and initiates a cancellation request on the cancellation token that all running tasks have access to.
  * StoppingGracefully: waiting for executing tasks to complete
  * Back to Stopped.
* RunAsync creates a background thread to evaluate rules. Cancel the RunAsync cancellation token or call RequestStop() to request shutdown.
* RunAsync waits for currently running callbacks to complete before returning. ShutdownTimeout defaults to 30 seconds; set it to Timeout.InfiniteTimeSpan to wait indefinitely.
* Scheduled callbacks should call RequestStop() if they need to stop the runtime. Await RunAsync() from application or host code to know when shutdown has completed.

## Catch-up behavior

By default, the runtime processes every scheduled second. If the evaluation loop falls behind because the process was paused, the machine was overloaded, or callbacks created enough pressure to delay evaluation, it will replay missed seconds in order. This can intentionally dispatch a large number of tasks; scheduled task implementations should be thread-safe and should apply their own non-overlap or singleton behavior when needed.

The runtime reports backlog episodes through `Trace` and the `ScheduleCatchUpWarning` event once the backlog reaches `CatchUpWarningThreshold` (60 seconds by default):

```C#
runtime.ScheduleCatchUpWarning += (sender, e) =>
{
  Console.WriteLine($"Scheduler backlog: {e.Backlog.TotalSeconds} seconds, policy={e.Policy}, skipped={e.SkippedSeconds}");
};
```

To skip older missed seconds instead of replaying the full backlog, opt into the skip policy:

```C#
runtime.CatchUpWarningThreshold = TimeSpan.FromSeconds(30);
runtime.CatchUpPolicy = ScheduleCatchUpPolicy.SkipToLatestSecond;
```

`SkipToLatestSecond` evaluates the most recent whole UTC second and advances from there; it does not dispatch callbacks for older skipped seconds.

## Schedule evaluation diagnostics

Invalid schedule inputs such as null callbacks, null tasks, null arrays, and null time zones are rejected when configuring a `ScheduleRule`. If an individual rule still throws while being evaluated, the runtime skips only that rule for that evaluated second, reports the failure through `Trace` and `ScheduleRuleEvaluationException`, and continues evaluating the remaining schedules.

```C#
runtime.ScheduleRuleEvaluationException += (sender, e) =>
{
  Console.WriteLine($"Schedule '{e.ScheduleRule.Name}' failed at {e.TimeEvaluatedUtc:o}: {e.Exception}");
};
```

Validation is basic, so it's possible to create rules that never fire, e.g., on day 31 of February.

## Changelog
* June 2026:
  * Updated supported target frameworks to .NET 8, .NET 9, and .NET 10.
  * Hardened unit tests with Codex 5.5.
  * Added catch-up policy.
  * Improved defensive schedule evalation, so invalid schedules, null tasks, or null timezones won't kill the runtime.
  * Improved stop logic with an optional timeout (defaults to 30s) to prevent deadlocks.
* June 2023:
  * Updated to include .NET 7.
  * Added cron string parsing.
  * Changed interface; use the runtime to CreateSchedule(), which will automatically add it to the runtime and update it on every configuration change. Instead of removing, call .AsActive(bool).
  * Added arbitrary timezone support (previously only local and UTC supported) [ref](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-7.0)

## A note on the 2010 vs 2021 versions

Circa 2010, this project lived on Codeplex and ran on .NET Framework 4. An old [version 1.0.0 still lives on Nuget](https://www.nuget.org/packages/TaskSchedulerEngine/1.0.0).
The 2021 edition of this project originally ran on .NET Core 3.1 and .NET 6. A lot has changed in the intervening years, namely how multithreaded programming
is accomplished in .NET (async/await didn't launch until C# 5.0 in 2012). While upgrading to modern .NET, I simplified the code, the end result being:
this library is incompatible with the 2010 version. While the core logic and the fluent API remain very similar, the
class names are incompatible, ITask has changed, and some of the multithreading behaviors are different.
This should be considered a *new* library that happens to share a name and some roots with the old one.
