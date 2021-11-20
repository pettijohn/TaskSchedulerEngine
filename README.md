# TaskSchedulerEngine

A lightweight (zero dependencies, ~400 lines of code) cron-like scheduler for in-memory scheduling of your code with second-level precision. Implement IScheduledTask or provide a callback, define a ScheduleRule, and Start the runtime. 
Schedule Rule evaluation is itself lightweight with bitwise evaluation of "now" against the rules. Targets .NET Core 3.1, .NET 5, and .NET 6. 

## Quick Start

See `sample/` in source tree for more detailed examples.

```C#
static async Task Main(string[] args)
{
  // Service Host is a helper to listen to AppDomain.ProcessExit and CTRL+C.
  // You may also instantiate TaskEvaluationRuntime directly.
  var host = new ServiceHost();

  // Use the fluent API to define a schedule rule.
  // Execute() accepts an IScheduledTask or Action<ScheduleRuleMatchEventArgs, CancellationToken>
  var s = new ScheduleRule()
    .WithName("EverySecond")
    .Execute((e, token) => {
      if(!token.IsCancellationRequested)
        Console.WriteLine("{0}: Event intended for {1:o} occured at {2:o}", e.TaskId, e.TimeScheduledUtc, e.TimeSignaledUtc);
    });

  // Add that schedule to the runtime.
  host.Runtime.AddSchedule(s);
  
  Console.WriteLine("Press CTRL+C to quit.");

  // Await the runtime.
  await host.Runtime.RunAsync();
}
```

## Terminology

* Schedule Rule - cron-like rule, with second-level precision. Leave a parameter unset to treate it as "*", otherwise set an int array for when you want to execute. 
* Scheduled Task - the thing to execute when schedule matches. The instance is shared by all executions forever and should be thread safe (unless you're completely sure there will only ever be at most one invocation). If you need an instance per execution, make ScheduledTask.OnScheduleRuleMatch a factory pattern.
* Schedule Rule Match - the current second ("Now") matches a Schedule Rule so the Scheduled Task should execute. A single ScheduleRule can only execute one ScheduledTask. If you need to execute multiple tasks sequentially, initiate them from your Task. Multiple Schedule Rules that fire at the same time will execute in parallel (order not guaranteed).
* Task Evaluation Runtime - the thing that evaluates the rules each second. Evaluation runs on its own thread and spawns Tasks on their own threads.

## Runtime Lifecycle

* Create a ServiceHost, await RunAsync, and you are guaranteed graceful shutdown.
* The ServiceHost is a helper that owns a TaskEvaluationRuntime. If you want to own the lifecycle, you may instantiate TaskEvaluationRuntimed directly.
* TaskEvaluationRuntime moves through four states: 
  * Stopped: nothing happening, can Start back into a running state.
  * Running: evaluating every second
  * StopRequested: instructs the every-second evaluation loop to quit
  * StoppingGracefully: waiting for executing tasks to complete
  * Back to Stopped.
* RunAsync creates a background thread to evaluate rules. RequestStop requests the background thread to stop. Control is then handed back to RunAsync which waits for all running tasks to complete. Then control is returned from RunAsync to the awaiting caller. 

## A note on the 2010 vs 2021 versions

Circa 2010, this project lived on Codeplex and ran on .NET Framework 4. An old [version 1.0.0 still lives on Nuget](https://www.nuget.org/packages/TaskSchedulerEngine/1.0.0). 
The 2021 edition of this project runs on .NET Core 3.1. A lot has changed in the intervening years, namely how multithreaded programming
is accomplished in .NET (async/await didn't launch until C# 5.0 in 2012). While upgrading to dotnet core, I simplified the code, the end result being:
this library is incompatible with the 2010 version. While the core logic and the fluent API remain very similar, the 
class names are incompatible, ITask has changed, and some of the multithreading behaviors are different. 
This should be considered a *new* library that happens to share a name and some roots with the old one. 


## TODO

- [x] Simplify thread locking code
- [x] Make ITask an object instance and not created every time
- [x] Verify multiple tasks invoked by same scheuldeRule execute in parallel - wrong, they are supposed to execute sequientially because they are += to schedule.ConditionsMet
- [x] ScheduleRule should only have a single Task to invoke. Make caller build their sequential logic into a Task. Simplifies multithreading understandability.
- [x] Remove TaskID (?) or replace it with Interlocked.Increment()~
- [x] Keep track of running tasks so they can gracefully shut down
- [x] Use Interlocked.Exchange to set running state of Pump - no, can't use with Enum without boxing/unboxing code, lock() is cleaner
- [x] Use async/await pattern
- [x] Remove singleton & scheduleRuntime 
- [x] Fix bug where you call AddSchedule() before Start() and it throws null ref 
- [#] Allow ScheduleRule.Execute to accept an Action<ScheduleRuleMatchEventArgs, CancellationToken>
- [x] Create "service host" that blocks and handles HUP/Kill/Restart events 
      - https://github.com/dotnet/runtime/issues/15178#issue-comment-box
      - https://docs.microsoft.com/en-us/dotnet/api/system.appdomain.processexit?view=net-6.0 
- [ ] Task exception handling  https://stackoverflow.com/questions/32067034/how-to-handle-task-run-exception/32067091
- [ ] Can a task unschedule itself? 
- [ ] Use strict mode to catch nulls 
- [ ] Improve unit tests
- [ ] Use proper logging with a Console sink 
- [x] Improve the pump loop to abort quickly rather than waiting 1s https://stackoverflow.com/questions/3460280/is-there-a-way-to-wake-a-sleeping-thread 
- [x] Task should be cancellable on shutdown 

- [ ] Use var in all definitions 
- [ ] Add year to support single execution
- [ ] Add expiration and on-start/on-stop methods. 



