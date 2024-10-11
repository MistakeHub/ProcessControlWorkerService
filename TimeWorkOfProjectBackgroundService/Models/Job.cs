using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimeWorkOfProjectBackgroundService.Models
{
    internal class Job
    {
        public string Title { get; set; }

        public TimeSpan TimeLimitation { get; set; }

        public TimeSpan RemainingTime { get; set; }

        public int Order { get; set; }
    }
}
