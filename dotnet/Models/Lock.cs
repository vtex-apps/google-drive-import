using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class Lock
    {
        [JsonProperty("import_started")]
        public DateTime ImportStarted { get; set; }
    }
}
