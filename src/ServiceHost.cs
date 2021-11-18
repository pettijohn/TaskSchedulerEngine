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
        /// and TODO service events like HUP/KILL
        /// </summary>
        public ServiceHost() 
        {
            Pump = new TaskEvaluationRuntime();
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs args)
            {
                // see also https://stackoverflow.com/questions/177856/how-do-i-trap-ctrl-c-sigint-in-a-c-sharp-console-app
                var stopSuccess = Pump.RequestStop();
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
        }

        public TaskEvaluationRuntime Pump { get; private set; }

        public async Task RunAsync()
        {
            await Pump.RunAsync();
        }
    }


}