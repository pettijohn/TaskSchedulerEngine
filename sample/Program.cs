using System;
using System.Threading;
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
            Console.WriteLine(String.Format("Event STOP  at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Main on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);

            var s = new ScheduleRule()
                .AtSeconds(0, 10, 20, 30, 40, 50, 60)
                .WithName("EveryTenSec")
                .Execute(new ConsoleWriter());
            SchedulerRuntime.Start(s);
            Console.WriteLine("Press CTRL+C to quit.");

            // More advanced example here https://stackoverflow.com/questions/177856/how-do-i-trap-ctrl-c-sigint-in-a-c-sharp-console-app
            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("CTRL+C detected, stopping.");
                SchedulerRuntime.Stop();
                return;
            };

            while (true) { }
        }
    }
}
