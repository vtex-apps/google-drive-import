using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class ProductResponse
    {
        [JsonProperty("Id")]
        public long Id { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("DepartmentId")]
        public long DepartmentId { get; set; }

        [JsonProperty("CategoryId")]
        public long CategoryId { get; set; }

        [JsonProperty("BrandId")]
        public long BrandId { get; set; }

        [JsonProperty("LinkId")]
        public string LinkId { get; set; }

        [JsonProperty("RefId")]
        public string RefId { get; set; }

        [JsonProperty("IsVisible")]
        public bool IsVisible { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("DescriptionShort")]
        public string DescriptionShort { get; set; }

        [JsonProperty("ReleaseDate")]
        public DateTimeOffset ReleaseDate { get; set; }

        [JsonProperty("KeyWords")]
        public object KeyWords { get; set; }

        [JsonProperty("Title")]
        public object Title { get; set; }

        [JsonProperty("IsActive")]
        public bool IsActive { get; set; }

        [JsonProperty("TaxCode")]
        public string TaxCode { get; set; }

        [JsonProperty("MetaTagDescription")]
        public object MetaTagDescription { get; set; }

        [JsonProperty("SupplierId")]
        public object SupplierId { get; set; }

        [JsonProperty("ShowWithoutStock")]
        public bool ShowWithoutStock { get; set; }

        [JsonProperty("ListStoreId")]
        public List<long> ListStoreId { get; set; }

        [JsonProperty("AdWordsRemarketingCode")]
        public object AdWordsRemarketingCode { get; set; }

        [JsonProperty("LomadeeCampaignCode")]
        public object LomadeeCampaignCode { get; set; }
    }
}
