using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TaskSchedulerEngine;

namespace sample
{
    public class ConsoleWriter : ITask
    {
        public void OnScheduleRuleMatch(object sender, ScheduleRuleMatchEventArgs e)
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
            Console.WriteLine("Main on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);

            var host = new ServiceHost();
            var s = new ScheduleRule()
                .WithName("EverySecond")
                .Execute(new ConsoleWriter());
            host.Pump.AddSchedule(s);
            var s2 = new ScheduleRule()
                .AtSeconds(0, 10, 20, 30, 40, 50, 60)
                .WithName("EveryTenSec")
                .Execute(new ConsoleWriteTask());
            host.Pump.AddSchedule(s2);
            Console.WriteLine("Press CTRL+C to quit.");

            var hostTask = host.RunAsync();
            await hostTask;
        }
    }
}
