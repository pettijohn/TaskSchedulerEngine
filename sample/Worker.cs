using TaskSchedulerEngine;

namespace sample;

public class ConsoleWriter : IScheduledTask
{
    public bool OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken _)
    {
        Console.WriteLine(String.Format("Event START at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
        // Sleep this thread for 12 seconds - it'll force the next invocation to occur on another thread. 
        System.Threading.Thread.Sleep(12000);
        Console.WriteLine(String.Format("Event STOP at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
        return true;
    }
}
public class Worker : BackgroundService
{
    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;

        //Internal messages on Trace, connect if you want
        //Trace.Listeners.Add(new ConsoleTraceListener());
        _logger.LogInformation("Main on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);

        Runtime = new TaskEvaluationRuntime();

        // Set up an unhandled exception handler for tasks
        Runtime.UnhandledScheduledTaskException = (Exception e) => 
            { _logger.LogError("Unhandled error on background thread", e); };

        // Schedule an instance of IScheduledTask
        Runtime.AddSchedule(new ScheduleRule()
            .WithName("EverySecond")
            .Execute(new ConsoleWriter()));
        
        // Use a Func<> instead of IScheduledTask (which gets wrapped in an AnonymousScheduledTask instance)
        Runtime.AddSchedule(new ScheduleRule()
            .AtSeconds(0, 10, 20, 30, 40, 50)
            .WithName("EveryTenSec")
            .Execute((e, token) => {
                if(!token.IsCancellationRequested)
                    _logger.LogInformation($"{e.TaskId}: Event intended for {e.TimeScheduledUtc:o} occured at {e.TimeSignaledUtc:o}");
                return true;
            }));

        // Unhandled exceptions need to be managed - see the handler in ctor 
        Runtime.AddSchedule(new ScheduleRule()
            .Execute((e, token) => {
                _logger.LogInformation("Execute once only, delete myself.");
                e.Runtime.DeleteSchedule(e.ScheduleRule);
                throw new Exception("This is an unhandled exception in a task, handle it with TaskSchedulerRuntime.UnhandledScheduledTaskException.");
            }));

        // Retry a task with exponential backoff. 
        Runtime.AddSchedule(new ScheduleRule()
            .ExecuteOnce(DateTime.UtcNow.AddSeconds(2))
            .Execute(new ExponentialBackoffTask((_, _) =>
            {
                // Do something that may fail like make a network call.
                // Gracefull fail by catching the exception and returning false.
                _logger.LogError("This task will fail and be retried 4 times.");
                return false;
            },
            4, // MaxAttempts
            2  // BaseRetryIntervalSeconds
               // Retry delay logic: baseRetrySeconds * (2^retryCount) 
               // In this case will retry after 2, 4, 8 second intervals  
            ))
        );
    }

    public TaskEvaluationRuntime Runtime { get; set; }
    private readonly ILogger<Worker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Runtime.RunAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await Runtime.StopAsync();
    }
}
