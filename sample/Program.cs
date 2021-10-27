using System;
using TaskSchedulerEngine;

namespace sample
{
    public class ConsoleWriter : ITask
    {
        public void HandleConditionsMetEvent(object sender, ConditionsMetEventArgs e)
        {
            Console.WriteLine(String.Format("Event START at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
            // Sleep this thread for 12 seconds - it'll force the next invocation to occur on another thread. 
            System.Threading.Thread.Sleep(12000);
            Console.WriteLine(String.Format("Event STOP  at {0} on thread {1}", e.TimeScheduledUtc, System.Threading.Thread.CurrentThread.ManagedThreadId));
        }
        
        /// <summary>
        /// Called after the constructor.
        /// </summary>
        public void Initialize(ScheduleDefinition schedule, object parameters)
        {
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Main on Thread " + System.Threading.Thread.CurrentThread.ManagedThreadId);
            var s = new Schedule()
                .AtSeconds(0, 10, 20, 30, 40, 50, 60)
                .WithName("EveryTenSec")
                .Execute<ConsoleWriter>();
            SchedulerRuntime.Start(s);
            Console.WriteLine("Press any key to quit.");
            var k = Console.Read();

            SchedulerRuntime.Stop();
        }
    }
}
