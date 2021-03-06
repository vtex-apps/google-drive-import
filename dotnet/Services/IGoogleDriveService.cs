﻿using DriveImport.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DriveImport.Services
{
    public interface IGoogleDriveService
    {
        Task<Token> RefreshGoogleAuthorizationToken(string refreshToken);
        Task<bool> RevokeGoogleAuthorizationToken();
        Task<string> GetGoogleAuthorizationUrl();
        Task<Token> GetGoogleToken();
        Task<GoogleWatch> SetWatch(string fileId, bool reset = false);
        Task<bool> ShareToken(Token token);

        Task<ListFilesResponse> ListFiles();
        Task<ListFilesResponse> GetFolders();
        Task<ListFilesResponse> ListImagesInRootFolder();
        Task<ListFilesResponse> ListImagesInFolder(string folderId, string pageToken = null);
        Task<ListFilesResponse> ListImages();
        Task<Dictionary<string, string>> ListFolders(string parentId = null);
        Task<string> CreateFolder(string folderName, string parentId = null);
        Task<bool> MoveFile(string fileId, string folderId);
        Task<byte[]> GetFile(string fileId);
        Task<bool> SetPermission(string fileId);
        Task<bool> RenameFile(string fileId, string fileName);
        Task<string> FindNewFolderId(string accountName);
        Task<string> SaveFile(StringBuilder file);
        Task<ListFilesResponse> ListSheetsInFolder(string folderId);
        Task<string> GetSheet(string fileId, string range);
        Task<UpdateValuesResponse> WriteSpreadsheetValues(string fileId, ValueRange valueRange);
        Task<string> CreateSpreadsheet(GoogleSheetCreate googleSheetRequest);
        Task<string> UpdateSpreadsheet(string fileId, BatchUpdate batchUpdate);

        Task<string> AddImagesToSheet();
        Task<string> ClearSpreadsheet(string fileId, SheetRange sheetRange);

        Task<string> CreateSheet();
        Task<string> GetSheetLink();
        Task<string> ClearAndAddImages();
    }
}