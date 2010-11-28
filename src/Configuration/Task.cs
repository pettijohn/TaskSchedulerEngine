using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace TaskSchedulerEngine.Configuration
{
    [XmlType("task")]
    public class Task
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("parameters")]
        public string Parameters { get; set; }
    }
}
