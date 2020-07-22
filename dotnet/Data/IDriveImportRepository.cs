using DriveImport.Models;
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
    }
}