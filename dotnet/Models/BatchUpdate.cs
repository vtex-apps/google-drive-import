using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class BatchUpdate
    {
        [JsonProperty("requests")]
        public Request[] Requests { get; set; }
    }

    public class Request
    {
        [JsonProperty("repeatCell", NullValueHandling = NullValueHandling.Ignore)]
        public RepeatCell RepeatCell { get; set; }

        [JsonProperty("updateSheetProperties", NullValueHandling = NullValueHandling.Ignore)]
        public UpdateSheetProperties UpdateSheetProperties { get; set; }
    }

    public class RepeatCell
    {
        [JsonProperty("range")]
        public BatchUpdateRange Range { get; set; }

        [JsonProperty("cell")]
        public Cell Cell { get; set; }

        [JsonProperty("fields")]
        public string Fields { get; set; }
    }

    public class Cell
    {
        [JsonProperty("userEnteredFormat")]
        public UserEnteredFormat UserEnteredFormat { get; set; }
    }

    public class UserEnteredFormat
    {
        [JsonProperty("backgroundColor")]
        public GroundColor BackgroundColor { get; set; }

        [JsonProperty("horizontalAlignment")]
        public string HorizontalAlignment { get; set; }

        [JsonProperty("textFormat")]
        public BatchUpdateTextFormat TextFormat { get; set; }
    }

    public class GroundColor
    {
        [JsonProperty("red")]
        public double Red { get; set; }

        [JsonProperty("green")]
        public double Green { get; set; }

        [JsonProperty("blue")]
        public double Blue { get; set; }
    }

    public class BatchUpdateTextFormat
    {
        [JsonProperty("foregroundColor")]
        public GroundColor ForegroundColor { get; set; }

        [JsonProperty("fontSize")]
        public long FontSize { get; set; }

        [JsonProperty("bold")]
        public bool Bold { get; set; }
    }

    public class BatchUpdateRange
    {
        [JsonProperty("sheetId")]
        public long SheetId { get; set; }

        [JsonProperty("startRowIndex")]
        public long StartRowIndex { get; set; }

        [JsonProperty("endRowIndex")]
        public long EndRowIndex { get; set; }
    }

    public class UpdateSheetProperties
    {
        [JsonProperty("properties")]
        public Properties Properties { get; set; }

        [JsonProperty("fields")]
        public string Fields { get; set; }
    }

    public class Properties
    {
        [JsonProperty("sheetId")]
        public long SheetId { get; set; }

        [JsonProperty("gridProperties")]
        public BatchUpdateGridProperties GridProperties { get; set; }
    }

    public class BatchUpdateGridProperties
    {
        [JsonProperty("frozenRowCount")]
        public long FrozenRowCount { get; set; }
    }
}
