using DriveImport.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IVtexAPIService
    {
        Task<string> DriveImport();
        Task<string> SheetImport();
        Task<string> GetProductIdFromReference(string productRefId);
        Task<string> GetSkuIdFromReference(string skuRefId);
        Task<List<string>> GetSkusFromProductId(string productId);
        Task<UpdateResponse> UpdateSkuImage(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl);
        Task<UpdateResponse> UpdateSkuImageArchive(string skuId, string imageName, string imageLabel, bool isMain, string imageId);
        Task<bool> UpdateSkuImageByFormData(string skuId, string imageName, string imageLabel, bool isMain, byte[] imageStream);
        Task<GetSkuContextResponse> GetSkuContext(string skuId);
        Task<UpdateResponse> ProcessImageFile(string fileName, string webLink);
        Task<bool> ProcessImageFile(string fileName, byte[] imageStream);
        Task<bool> DeleteImageByName(string skuId, string imageName);
        Task<bool> DeleteSkuImages(string skuId);
        Task<bool> ProcessDelete(string identificatorType, string id, string imageName);
    }
}