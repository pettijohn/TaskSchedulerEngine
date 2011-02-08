/* 
 * Task Scheduler Engine
 * Released under the BSD License
 * http://taskschedulerengine.codeplex.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace TaskSchedulerEngine.Configuration
{
    public class TaskSchedulerEngineConfigurationSection : ConfigurationSection
    {
        protected override void DeserializeSection(System.Xml.XmlReader reader)
        {
            System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(TaskSchedulerEngineSection));
            engine = (TaskSchedulerEngineSection)serializer.Deserialize(reader);
        }

        private TaskSchedulerEngineSection engine;

        public List<At> Schedule
        {
            get
            {
                return engine.Schedule;
            }
        }
    }
}
