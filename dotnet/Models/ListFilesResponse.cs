using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class ListFilesResponse
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("incompleteSearch")]
        public bool IncompleteSearch { get; set; }

        [JsonProperty("files")]
        public List<File> Files { get; set; }
    }

    public class File
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("mimeType")]
        public string MimeType { get; set; }
    }
}
