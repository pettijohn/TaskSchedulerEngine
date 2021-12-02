using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TaskSchedulerEngine;

namespace sample
{
    public class ConsoleWriter : IScheduledTask
    {
        public void OnScheduleRuleMatch(ScheduleRuleMatchEventArgs e, CancellationToken _)
        {
            Console.WriteLine(String.Format("Event START at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
            // Sleep this thread for 12 seconds - it'll force the next invocation to occur on another thread. 
            System.Threading.Thread.Sleep(12000);
            Console.WriteLine(String.Format("Event STOP at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {   
            //Internal messages on Trace, connect if you want
            //Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.WriteLine("Main on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);

            var host = new ServiceHost();
            host.Runtime.AddSchedule(new ScheduleRule()
                .WithName("EverySecond")
                .Execute(new ConsoleWriter()));
            
            host.Runtime.AddSchedule(new ScheduleRule()
                .AtSeconds(0, 10, 20, 30, 40, 50)
                .WithName("EveryTenSec")
                .Execute((e, token) => {
                    if(!token.IsCancellationRequested)
                        Console.WriteLine("{0}: Event intended for {1:o} occured at {2:o}", e.TaskId, e.TimeScheduledUtc, e.TimeSignaledUtc);
                }));

            host.Runtime.AddSchedule(new ScheduleRule()
                .Execute((e, token) => {
                    Console.WriteLine("Execute once only, delete myself.");
                    e.Runtime.DeleteSchedule(e.ScheduleRule);
                    throw new Exception("This is an unhandled exception in a task, handle it with TaskSchedulerRuntime.UnhandledScheduledTaskException.");
                }));


            
            Console.WriteLine("Press CTRL+C to quit.");

            // Set up an unhandled exception handler for tasks
            host.Runtime.UnhandledScheduledTaskException = (Exception e) => 
                { Console.WriteLine(e.Message); };

            var hostTask = host.Runtime.RunAsync();
            await hostTask;
        }
    }
}
