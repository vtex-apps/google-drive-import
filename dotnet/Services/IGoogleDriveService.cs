﻿using DriveImport.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IGoogleDriveService
    {
        Task<Token> GetGoogleAuthorizationToken(string code);
        Task<Token> RefreshGoogleAuthorizationToken(string refreshToken);
        Task<bool> RevokeGoogleAuthorizationToken();
        Task<string> GetGoogleAuthorizationUrl();
        Task<bool> ProcessReturn(string code);
        Task SaveCredentials(Credentials credentials);
        Task<Token> GetGoogleToken();
        Task<GoogleWatch> SetWatch(string fileId);

        Task<ListFilesResponse> ListFiles();
        Task<ListFilesResponse> ListImagesInRootFolder();
        Task<ListFilesResponse> ListImagesInFolder(string folderId);
        Task<ListFilesResponse> ListImages();
        Task<Dictionary<string, string>> ListFolders(string parentId = null);
        Task<bool> CreateFolder(string folderName, string parentId = null);
        Task<bool> MoveFile(string fileId, string folderId);
        Task<byte[]> GetFile(string fileId);
        Task<bool> SetPermission(string fileId);
        Task<bool> RenameFile(string fileId, string fileName);
    }
}