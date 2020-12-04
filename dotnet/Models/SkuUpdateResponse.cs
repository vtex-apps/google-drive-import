using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class SkuUpdateResponse
    {
        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("SkuId")]
        public long SkuId { get; set; }

        [JsonProperty("IsMain")]
        public bool IsMain { get; set; }

        [JsonProperty("Label")]
        public string Label { get; set; }
    }
}
