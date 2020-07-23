using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public partial class GoogleWatch
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("resourceId")]
        public string ResourceId { get; set; }

        [JsonProperty("resourceUri")]
        public string ResourceUri { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        //[JsonProperty("expiration")]
        //public long Expiration { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        //[JsonProperty("payload")]
        //public bool Payload { get; set; }

        //[JsonProperty("params")]
        //public object Params { get; set; }
    }
}
