using System;
using System.Collections.Generic;
using System.Text;

namespace DriveImport.Models
{
    public class WatchExpiration
    {
        public string FolderId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
