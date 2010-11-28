using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace TaskSchedulerEngine.Configuration
{
    [XmlType("at")]
    public class At
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        /// <summary>
        /// "UTC" or "Local"
        /// </summary>
        [XmlAttribute("kind")]
        public DateTimeKind Kind { get; set; }
        
        [XmlAttribute("month")]
        public string Month { get; set; }
        
        [XmlAttribute("dayOfMonth")]
        public string DayOfMonth { get; set; }
        
        /// <summary>
        /// Day of week, numerically, where 0 = Sunday and 6 = Saturday. 
        /// </summary>
        [XmlAttribute("dayOfWeek")]
        public string DayOfWeek { get; set; }
        
        [XmlAttribute("hour")]
        public string Hour { get; set; }
        
        [XmlAttribute("minute")]
        public string Minute { get; set; }
        
        [XmlAttribute("second")]
        public string Second { get; set; }

        [XmlArray("execute")]
        public List<Task> Execute { get; set; }
    }
}
