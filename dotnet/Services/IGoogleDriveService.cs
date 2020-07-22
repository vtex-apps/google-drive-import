using DriveImport.Models;
using System.Collections.Generic;
using System.IO;
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
        Task<Token> GetGoogleToken();

        Task<string> ListFiles();
        Task<ListFilesResponse> ListImagesInRootFolder();
        Task<ListFilesResponse> ListImagesInFolder(string folderId);
        Task<ListFilesResponse> ListImages();
        Task<Dictionary<string, string>> ListFolders();
        Task<bool> CreateFolder(string folderName);
        Task<bool> MoveFile(string fileId, string folderId);
        Task<byte[]> GetFile(string fileId);
        Task<bool> SetPermission(string fileId);
    }
}