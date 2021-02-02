using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class GoogleSheetCreate
    {
        [JsonProperty("spreadsheetId")]
        public string SpreadsheetId { get; set; }

        [JsonProperty("properties")]
        public GoogleSheetProperties Properties { get; set; }

        [JsonProperty("sheets")]
        public Sheet[] Sheets { get; set; }

        [JsonProperty("spreadsheetUrl")]
        public Uri SpreadsheetUrl { get; set; }
    }

    public class GoogleSheetProperties
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("autoRecalc")]
        public string AutoRecalc { get; set; }

        [JsonProperty("timeZone")]
        public string TimeZone { get; set; }

        [JsonProperty("defaultFormat")]
        public DefaultFormat DefaultFormat { get; set; }

        [JsonProperty("spreadsheetTheme")]
        public SpreadsheetTheme SpreadsheetTheme { get; set; }
    }

    public class DefaultFormat
    {
        [JsonProperty("backgroundColor")]
        public BackgroundColorClass BackgroundColor { get; set; }

        [JsonProperty("padding")]
        public Padding Padding { get; set; }

        [JsonProperty("verticalAlignment")]
        public string VerticalAlignment { get; set; }

        [JsonProperty("wrapStrategy")]
        public string WrapStrategy { get; set; }

        [JsonProperty("textFormat")]
        public TextFormat TextFormat { get; set; }

        [JsonProperty("backgroundColorStyle")]
        public BackgroundColorStyle BackgroundColorStyle { get; set; }
    }

    public class BackgroundColorClass
    {
        [JsonProperty("red", NullValueHandling = NullValueHandling.Ignore)]
        public double? Red { get; set; }

        [JsonProperty("green", NullValueHandling = NullValueHandling.Ignore)]
        public double? Green { get; set; }

        [JsonProperty("blue", NullValueHandling = NullValueHandling.Ignore)]
        public double? Blue { get; set; }
    }

    public class BackgroundColorStyle
    {
        [JsonProperty("rgbColor")]
        public BackgroundColorClass RgbColor { get; set; }
    }

    public class Padding
    {
        [JsonProperty("top")]
        public long Top { get; set; }

        [JsonProperty("right")]
        public long Right { get; set; }

        [JsonProperty("bottom")]
        public long Bottom { get; set; }

        [JsonProperty("left")]
        public long Left { get; set; }
    }

    public class TextFormat
    {
        [JsonProperty("foregroundColor")]
        public ForegroundColorClass ForegroundColor { get; set; }

        [JsonProperty("fontFamily")]
        public string FontFamily { get; set; }

        [JsonProperty("fontSize")]
        public long FontSize { get; set; }

        [JsonProperty("bold")]
        public bool Bold { get; set; }

        [JsonProperty("italic")]
        public bool Italic { get; set; }

        [JsonProperty("strikethrough")]
        public bool Strikethrough { get; set; }

        [JsonProperty("underline")]
        public bool Underline { get; set; }

        [JsonProperty("foregroundColorStyle")]
        public ForegroundColorStyle ForegroundColorStyle { get; set; }
    }

    public class ForegroundColorClass
    {
    }

    public class ForegroundColorStyle
    {
        [JsonProperty("rgbColor")]
        public ForegroundColorClass RgbColor { get; set; }
    }

    public class SpreadsheetTheme
    {
        [JsonProperty("primaryFontFamily")]
        public string PrimaryFontFamily { get; set; }

        [JsonProperty("themeColors")]
        public ThemeColor[] ThemeColors { get; set; }
    }

    public class ThemeColor
    {
        [JsonProperty("colorType")]
        public string ColorType { get; set; }

        [JsonProperty("color")]
        public BackgroundColorStyle Color { get; set; }
    }

    public class Sheet
    {
        [JsonProperty("properties")]
        public SheetProperties Properties { get; set; }
    }

    public class SheetProperties
    {
        [JsonProperty("sheetId")]
        public long SheetId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("index")]
        public long Index { get; set; }

        [JsonProperty("sheetType")]
        public string SheetType { get; set; }

        [JsonProperty("gridProperties")]
        public GridProperties GridProperties { get; set; }
    }

    public class GridProperties
    {
        [JsonProperty("rowCount")]
        public long RowCount { get; set; }

        [JsonProperty("columnCount")]
        public long ColumnCount { get; set; }
    }
}
