/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * http://taskschedulerengine.codeplex.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace TaskSchedulerEngine.Configuration
{
    [XmlType("taskSchedulerEngine")]
    public class TaskSchedulerEngineSection
    {
        [XmlArray("schedule")]
        public List<At> Schedule { get; set; }
    }
}
