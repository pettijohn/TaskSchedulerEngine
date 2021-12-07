/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * https://github.com/pettijohn/TaskSchedulerEngine
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TaskSchedulerEngine
{
    public class ServiceHost
    {
        /// <summary>
        /// One line startup that listens to CTRL+C 
        /// and AppDomain.ProcessExit. 
        /// </summary>
        public ServiceHost(TaskEvaluationRuntime runtime) 
        {
            Runtime = runtime;
            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs args) =>
            {
                // see also https://stackoverflow.com/questions/177856/how-do-i-trap-ctrl-c-sigint-in-a-c-sharp-console-app
                var stopSuccess = Runtime.RequestStop();
                args.Cancel = stopSuccess;
                if (stopSuccess)
                {
                    Console.WriteLine("CTRL+C detected, stopping gracefully.");
                    Console.WriteLine("Press CTRL+C to abort immediately.");
                }
                else
                {
                    Console.WriteLine("Aborting immediately.");
                }
            };
            AppDomain.CurrentDomain.ProcessExit += (object? sender, EventArgs e) =>
            {
                Trace.WriteLine("AppDomain unloading, requesting graceful shutdown", "TaskSchedulerEngine");
                Runtime.RequestStop();
            };
        }

        public ServiceHost() : this(new TaskEvaluationRuntime())
        {
        }

        public TaskEvaluationRuntime Runtime { get; private set; }
    }


}