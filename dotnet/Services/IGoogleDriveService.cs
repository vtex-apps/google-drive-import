using DriveImport.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IGoogleDriveService
    {
        Task<Token> GetGoogleAuthorizationToken(string code);
        Task<Token> RefreshGoogleAuthorizationToken(string refreshToken);
        Task<string> GetGoogleAuthorizationUrl();
        Task<bool> ProcessReturn(string code);
        Task SaveCredentials(Credentials credentials);

        Task<string> ListFiles();
        Task<ListFilesResponse> ListImagesInRootFolder();
        Task<ListFilesResponse> ListImagesInFolder(string folderId);
        Task<ListFilesResponse> ListImages();
        Task<Dictionary<string, string>> ListFolders();
        Task<bool> CreateFolder(string folderName);
        Task<bool> MoveFile(string fileId, string folderId);
    }
}