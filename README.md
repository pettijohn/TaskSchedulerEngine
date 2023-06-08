# TaskSchedulerEngine

A lightweight (zero dependencies, <400 lines of code) cron-like scheduler for in-memory scheduling of your code with second-level precision. 
Implement IScheduledTask or provide a callback, define a ScheduleRule, and Start the runtime. 
Schedule Rule evaluation is itself lightweight with bitwise evaluation of "now" against the rules (see ScheduleRuleEvaluationOptimized). 
Each invoked ScheduledTask runs on its own thread so long running tasks won't block other tasks. 
Targets .NET Core 3.1, .NET 6 (and presumably everything in between).

## Quick Start

See `sample/` in source tree for more detailed examples.

`dotnet add package TaskSchedulerEngine`

Nuget link: https://www.nuget.org/packages/TaskSchedulerEngine/

Version number scheme is (two digit year).(day of year).(hour).(minute).

```C#
static async Task Main(string[] args)
{
  // Instantiate TaskEvaluationRuntime.
  var runtime = new TaskEvaluationRuntime();

  // Use the fluent API to define a schedule rule, or set the corresponding properties
  // Execute() accepts an IScheduledTask or Action<ScheduleRuleMatchEventArgs, CancellationToken, bool>
  var s1 = runtime.CreateSchedule()
    .AtSeconds(0, 10, 20, 30, 40, 50)
    // .AtMonths(), .AtDays(), AtDaysOfWeek() ... etc
    .WithName("EveryTenSec") //Optional ID for your reference 
    .Execute((e, token) => {
      if(!token.IsCancellationRequested)
        Console.WriteLine($"{e.TaskId}: Event intended for {e.TimeScheduledUtc:o} occurred at {e.TimeScheduledUtc:o}");
        return true; // Return success. Used by retry scenarios. 
    });

  var s2 = runtime.CreateSchedule()
    .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(5))
    .Execute((_, _) => { Console.WriteLine("Use ExecuteOnceAt to run this task in 5 seconds. Useful for retry scenarios."); return true; });

  var s3 = runtime.CreateSchedule()
    .ExecuteOnceAt(DateTimeOffset.UtcNow.AddSeconds(1))
    .ExecuteAndRetry(
      (e, _) => { 
          // Do something that may fail like a network call - catch & gracefully fail by returning false.
          // Exponential backoff task will retry up to MaxAttempts times. 
          return false; 
      },
      4, // MaxAttempts
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
    .Execute((e, token) => {
      if(!token.IsCancellationRequested)
        Console.WriteLine($"Load me from config and change me without recompiling!");
        return true; 
    });
  
  // Handle the shutdown event (CTRL+C, SIGHUP) if graceful shutdown desired
  AppDomain.CurrentDomain.ProcessExit += (s, e) => runtime.RequestStop();

  // Await the runtime.
  await runtime.RunAsync();

  // Listen for some signal to quit
  Thread.Sleep(30000);
  
  // Graceful shutdown. Request a stop and await running tasks.
  await runtime.StopAsync();
}
```

## Terminology

* Schedule Rule - cron-like rule, with second-level precision. Leave a parameter unset to treat it as "*", otherwise set an int array for when you want to execute. 
* Scheduled Task - the thing to execute when schedule matches. The instance is shared by all executions forever and should be thread safe (unless you're completely sure there will only ever be at most one invocation). If you need an instance per execution, make ScheduledTask.OnScheduleRuleMatch a factory pattern.
* Schedule Rule Match - the current second ("Now") matches a Schedule Rule so the Scheduled Task should execute. A single ScheduleRule can only execute one ScheduledTask. If you need to execute multiple tasks sequentially, initiate them from your Task. Multiple Schedule Rules that fire at the same time will execute in parallel (order not guaranteed).
* Task Evaluation Runtime - the thing that evaluates the rules each second. Evaluation runs on its own thread and spawns Tasks on their own threads.

## Runtime Lifecycle

* Instantiate TaskEvaluationRuntime and use RunAsync(), optionally RequestStop(), and StopAsync() for start and graceful shutdown.
* TaskEvaluationRuntime moves through four states: 
  * Stopped: nothing happening, can Start back into a running state.
  * Running: evaluating every second
  * StopRequested: instructs the every-second evaluation loop to quit and initiates a cancellation request on the cancellation token that all running tasks have access to. 
  * StoppingGracefully: waiting for executing tasks to complete
  * Back to Stopped.
* RunAsync creates a background thread to evaluate rules. RequestStop requests the background thread to stop. Control is then handed back to RunAsync which waits for all running tasks to complete. Then control is returned from RunAsync to the awaiting caller. 

Validation is basic, so it's possible to create rules that never fire, e.g., on day 31 of February. 

## Changes
* June 2023: 
  * Updated to .NET 7. 
  * Added cron string parsing.
  * Changed interface; use the runtime to CreateSchedule(), which will automatically add it to the runtime and update it on every configuration change. 

## A note on the 2010 vs 2021 versions

Circa 2010, this project lived on Codeplex and ran on .NET Framework 4. An old [version 1.0.0 still lives on Nuget](https://www.nuget.org/packages/TaskSchedulerEngine/1.0.0). 
The 2021 edition of this project runs on .NET Core 3.1 and .NET 6. A lot has changed in the intervening years, namely how multithreaded programming
is accomplished in .NET (async/await didn't launch until C# 5.0 in 2012). While upgrading .NET 6, I simplified the code, the end result being:
this library is incompatible with the 2010 version. While the core logic and the fluent API remain very similar, the 
class names are incompatible, ITask has changed, and some of the multithreading behaviors are different. 
This should be considered a *new* library that happens to share a name and some roots with the old one. 
