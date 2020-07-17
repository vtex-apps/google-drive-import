using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IVtexAPIService
    {
        Task<string> GetProductIdFromReference(string productRefId);
        Task<string> GetSkuIdFromReference(string skuRefId);
        Task<List<string>> GetSkusFromProductId(string productId);
        Task<bool> UpdateSkuImage(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl);
    }
}