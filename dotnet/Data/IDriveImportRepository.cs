using DriveImport.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveImport.Data
{
    public interface IDriveImportRepository
    {
        Task<Credentials> GetCredentials();
        Task<Token> LoadToken();
        Task SaveCredentials(Credentials credentials);
        Task<bool> SaveToken(Token token);
        Task<MerchantSettings> GetMerchantSettings();
        Task SetImportLock(DateTime importStartTime);
        Task<DateTime> CheckImportLock();
        Task ClearImportLock();
        Task<IList<WatchExpiration>> GetWatchExpiration();
        Task<WatchExpiration> GetWatchExpiration(string folderId);
        Task SetWatchExpiration(WatchExpiration watchExpiration);
        Task<FolderIds> LoadFolderIds(string accountName);
        Task<bool> SaveFolderIds(FolderIds folderIds, string accountName);
    }
}