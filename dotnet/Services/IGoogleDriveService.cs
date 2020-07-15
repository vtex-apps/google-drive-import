using DriveImport.Models;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IGoogleDriveService
    {
        Task<Token> GetGoogleAuthorizationToken(string code);
        Task<string> GetGoogleAuthorizationUrl();
        Task<bool> ProcessReturn(string code);
        Task SaveCredentials(Credentials credentials);
    }
}