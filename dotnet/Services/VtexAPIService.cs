using DriveImport.Data;
using DriveImport.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vtex.Api.Context;

namespace DriveImport.Services
{
    public class VtexAPIService : IVtexAPIService
    {
        private readonly IIOServiceContext _context;
        private readonly IVtexEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IDriveImportRepository _driveImportRepository;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly string _applicationName;

        public VtexAPIService(IIOServiceContext context, IVtexEnvironmentVariableProvider environmentVariableProvider, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory, IDriveImportRepository driveImportRepository, IGoogleDriveService googleDriveService)
        {
            this._context = context ??
                            throw new ArgumentNullException(nameof(context));

            this._environmentVariableProvider = environmentVariableProvider ??
                                                throw new ArgumentNullException(nameof(environmentVariableProvider));

            this._httpContextAccessor = httpContextAccessor ??
                                        throw new ArgumentNullException(nameof(httpContextAccessor));

            this._clientFactory = clientFactory ??
                               throw new ArgumentNullException(nameof(clientFactory));

            this._driveImportRepository = driveImportRepository ??
                               throw new ArgumentNullException(nameof(driveImportRepository));

            this._googleDriveService = googleDriveService ??
                               throw new ArgumentNullException(nameof(googleDriveService));

            this._applicationName =
                $"{this._environmentVariableProvider.ApplicationVendor}.{this._environmentVariableProvider.ApplicationName}";
        }

        public async Task<string> DriveImport()
        {
            // check lock
            DateTime importStarted = await _driveImportRepository.CheckImportLock();

            Console.WriteLine($"Check lock: {importStarted}");

            TimeSpan elapsedTime = DateTime.Now - importStarted;
            if (elapsedTime.TotalHours < DriveImportConstants.LOCK_TIMEOUT)
            {
                Console.WriteLine("Blocked by lock");
                _context.Vtex.Logger.Info("DriveImport", null, $"Blocked by lock.  Import started: {importStarted}");
                return ($"Import started {importStarted} in progress.");
            }

            await _driveImportRepository.SetImportLock(DateTime.Now);
            Console.WriteLine($"Set new lock: {DateTime.Now}");
            _context.Vtex.Logger.Info("DriveImport", null, $"Set new lock: {DateTime.Now}");

            Console.WriteLine("Drive import started");
            bool updated = false;
            int doneCount = 0;
            int errorCount = 0;
            List<string> doneFileNames = new List<string>();
            List<string> errorFileNames = new List<string>();
            //TimeSpan timeSpan = new TimeSpan(0, 30, 0);

            string importFolderId = null;
            string accountFolderId = null;
            string imagesFolderId = null;
            string newFolderId = null;
            string doneFolderId = null;
            string errorFolderId = null;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                importFolderId = folderIds.ImagesFolderId;
                accountFolderId = folderIds.AccountFolderId;
                imagesFolderId = folderIds.ImagesFolderId;
                newFolderId = folderIds.NewFolderId;
                doneFolderId = folderIds.DoneFolderId;
                errorFolderId = folderIds.ErrorFolderId;
            }
            else
            {
                ListFilesResponse getFoldersResponse = await _googleDriveService.GetFolders();
                if (getFoldersResponse != null)
                {
                    importFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.IMPORT)).Select(f => f.Id).FirstOrDefault();
                    if (!string.IsNullOrEmpty(importFolderId))
                    {
                        //Console.WriteLine($"importFolderId:{importFolderId}");
                        accountFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(accountName) && f.Parents.Contains(importFolderId)).Select(f => f.Id).FirstOrDefault();
                        if (!string.IsNullOrEmpty(accountFolderId))
                        {
                            //Console.WriteLine($"accountFolderId:{accountFolderId}");
                            imagesFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.IMAGES) && f.Parents.Contains(accountFolderId)).Select(f => f.Id).FirstOrDefault();
                            if (!string.IsNullOrEmpty(imagesFolderId))
                            {
                                //Console.WriteLine($"imagesFolderId:{imagesFolderId}");
                                newFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.NEW) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                doneFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.DONE) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                errorFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.ERROR) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                //Console.WriteLine($"n:{newFolderId} d:{doneFolderId} e:{errorFolderId}");
                            }
                        }
                    }
                }
            }

            // If any essential folders are missing verify and create the folder structure.
            if (string.IsNullOrEmpty(newFolderId) || string.IsNullOrEmpty(doneFolderId) || string.IsNullOrEmpty(errorFolderId))
            {
                folderIds = null;
                _context.Vtex.Logger.Info("DriveImport", null, "Verifying folder structure.");
                Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

                if (folders == null)
                {
                    return ($"Error accessing Drive.");
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
                {
                    importFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                }
                else
                {
                    importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
                }

                if (string.IsNullOrEmpty(importFolderId))
                {
                    _context.Vtex.Logger.Info("DriveImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                    return ($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
                }

                folders = await _googleDriveService.ListFolders(importFolderId);

                if (!folders.ContainsValue(accountName))
                {
                    accountFolderId = await _googleDriveService.CreateFolder(accountName, importFolderId);
                }
                else
                {
                    accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
                }

                if (string.IsNullOrEmpty(accountFolderId))
                {
                    _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {accountFolderId} folder");
                    return ($"Could not find {accountFolderId} folder");
                }

                folders = await _googleDriveService.ListFolders(accountFolderId);

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
                {
                    imagesFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                }
                else
                {
                    imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
                }

                if (string.IsNullOrEmpty(imagesFolderId))
                {
                    _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {imagesFolderId} folder");
                    return ($"Could not find {imagesFolderId} folder");
                }

                folders = await _googleDriveService.ListFolders(imagesFolderId);

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
                {
                    newFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
                    bool setPermission = await _googleDriveService.SetPermission(newFolderId);
                    if (!setPermission)
                    {
                        _context.Vtex.Logger.Error("DriveImport", "SetPermission", $"Could not set permissions on '{DriveImportConstants.FolderNames.NEW}' folder {newFolderId}");
                    }
                }
                else
                {
                    newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
                {
                    doneFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
                }
                else
                {
                    doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
                {
                    errorFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
                }
                else
                {
                    errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;
                }
            }

            if (folderIds == null)
            {
                folderIds = new FolderIds
                {
                    AccountFolderId = accountFolderId,
                    DoneFolderId = doneFolderId,
                    ErrorFolderId = errorFolderId,
                    ImagesFolderId = imagesFolderId,
                    ImportFolderId = imagesFolderId,
                    NewFolderId = newFolderId
                };

                await _driveImportRepository.SaveFolderIds(folderIds, accountName);
            }

            Dictionary<string, string> images = new Dictionary<string, string>();
            //StringBuilder results = new StringBuilder("Sku,Name,Label,isMain,Status,Message");
            List<string> results = new List<string>();
            results.Add("Sku,Name,Label,isMain,Status,Message");

            GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId);

            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            if (imageFiles != null)
            {
                //bool thereAreFiles = imageFiles.Files.Count > 0;
                bool thereAreFiles = imageFiles.Files.Any(f => f.Name.Contains(","));
                int skipCount = 0;
                while (thereAreFiles)
                {
                    bool moveFailed = false;
                    bool didSkip = false;
                    _context.Vtex.Logger.Info("DriveImport", null, $"Processing {imageFiles.Files.Count} files.");
                    foreach (GoogleFile file in imageFiles.Files)
                    {
                        if (file.Name.Contains(","))
                        {
                            _context.Vtex.Logger.Info("DriveImport", null, $"Beginning Processing of '{file.Name}' at {file.WebContentLink}");
                            if (!string.IsNullOrEmpty(file.WebContentLink.ToString()))
                            {
                                UpdateResponse updateResponse = await this.ProcessImageFile(file.Name, file.WebContentLink.ToString());
                                _context.Vtex.Logger.Info("DriveImport", "UpdateResponse", $"'{file.Name}' Response: {JsonConvert.SerializeObject(updateResponse)}");
                                results.AddRange(updateResponse.Results);
                                updated = updateResponse.Success;
                                bool moved = false;
                                if (updated)
                                {
                                    doneCount++;
                                    doneFileNames.Add(file.Name);
                                    moved = await _googleDriveService.MoveFile(file.Id, doneFolderId);
                                    if (!moved)
                                    {
                                        _context.Vtex.Logger.Error("DriveImport", "MoveFile", $"Failed to move '{file.Name}' to folder '{DriveImportConstants.FolderNames.DONE}'");
                                        string newFileName = $"Move_to_{DriveImportConstants.FolderNames.DONE}_{file.Name}";
                                        await _googleDriveService.RenameFile(file.Id, newFileName);
                                        moveFailed = true;
                                    }
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(updateResponse.StatusCode) && updateResponse.StatusCode.Equals(DriveImportConstants.GATEWAY_TIMEOUT))
                                    {
                                        didSkip = true;
                                        _context.Vtex.Logger.Warn("DriveImport", null, $"Skipping {file.Name} {JsonConvert.SerializeObject(updateResponse)}");
                                    }
                                    else
                                    {
                                        errorCount++;
                                        errorFileNames.Add(file.Name);
                                        moved = await _googleDriveService.MoveFile(file.Id, errorFolderId);
                                        string errorText = updateResponse.Message.Replace(" ", "_").Replace("\"", "");
                                        string newFileName = $"{errorText}-{file.Name}";
                                        await _googleDriveService.RenameFile(file.Id, newFileName);
                                        if (!moved)
                                        {
                                            _context.Vtex.Logger.Error("DriveImport", "MoveFile", $"Failed to move '{file.Name}' to folder '{DriveImportConstants.FolderNames.ERROR}'");
                                            newFileName = $"Move_to_{DriveImportConstants.FolderNames.ERROR}_{newFileName}";
                                            await _googleDriveService.RenameFile(file.Id, newFileName);
                                            moveFailed = true;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Skipping '{file.Name}' - no commas");
                            _context.Vtex.Logger.Debug("DriveImport", null, $"Skipping '{file.Name}' - no commas");
                        }
                    }

                    if (moveFailed)
                    {
                        thereAreFiles = false;
                    }
                    else
                    {
                        await Task.Delay(10000);
                        imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
                        if (imageFiles == null)
                        {
                            thereAreFiles = false;
                        }
                        else
                        {
                            //thereAreFiles = imageFiles.Files.Count > 0;
                            thereAreFiles = imageFiles.Files.Any(f => f.Name.Contains(","));
                        }

                        if (thereAreFiles && didSkip)
                        {
                            if (skipCount > 10)
                            {
                                // Prevent endless loop
                                thereAreFiles = false;
                            }
                            else
                            {
                                skipCount++;
                            }
                        }
                    }

                    Console.WriteLine($"Loop again? {thereAreFiles}");
                }
            }

            if (doneCount + errorCount > 0)
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Imported {doneCount} image(s).  {errorCount} image(s) not imported.  Done:[{string.Join(" ", doneFileNames)}] Error:[{string.Join(" ", errorFileNames)}]");
                _context.Vtex.Logger.Info("DriveImport", "Results", JsonConvert.SerializeObject(results));
            }

            if (errorCount > 0)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (string textLine in results)
                    {
                        sb.AppendLine(textLine);
                    }

                    string fileId = await _googleDriveService.SaveFile(sb);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        await _googleDriveService.RenameFile(fileId, $"ImportErrors_{DateTime.Now.Date}");
                        await _googleDriveService.MoveFile(fileId, errorFolderId);
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("DriveImport", "Results", $"Error saving error list", ex);
                }
            }

            await ClearLockAfterDelay(5000);

            return ($"Imported {doneCount} image(s).  {errorCount} image(s) not imported.");
        }

        public async Task<string> SheetImport()
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            // check lock
            DateTime importStarted = await _driveImportRepository.CheckImportLock();

            //Console.WriteLine($"Check lock: {importStarted}");

            TimeSpan elapsedTime = DateTime.Now - importStarted;
            if (elapsedTime.TotalHours < DriveImportConstants.LOCK_TIMEOUT)
            {
                Console.WriteLine("Blocked by lock");
                _context.Vtex.Logger.Info("SheetImport", null, $"Blocked by lock.  Import started: {importStarted}");
                return ($"Import started {importStarted} in progress.");
            }

            await _driveImportRepository.SetImportLock(DateTime.Now);
            //Console.WriteLine($"Set new lock: {DateTime.Now}");
            _context.Vtex.Logger.Info("SheetImport", null, $"Set new lock: {DateTime.Now}");

            Console.WriteLine($"Import from Spreadsheet started.  [{stopWatch.ElapsedMilliseconds}]");
            bool updated = false;
            int doneCount = 0;
            int errorCount = 0;
            List<string> doneFileNames = new List<string>();
            List<string> errorFileNames = new List<string>();
            List<string> doneFileIds = new List<string>();
            List<string> errorFileIds = new List<string>();
            //TimeSpan timeSpan = new TimeSpan(0, 30, 0);

            string importFolderId = null;
            string accountFolderId = null;
            string imagesFolderId = null;
            string newFolderId = null;
            string doneFolderId = null;
            string errorFolderId = null;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                importFolderId = folderIds.ImagesFolderId;
                accountFolderId = folderIds.AccountFolderId;
                imagesFolderId = folderIds.ImagesFolderId;
                newFolderId = folderIds.NewFolderId;
                doneFolderId = folderIds.DoneFolderId;
                errorFolderId = folderIds.ErrorFolderId;
            }
            else
            {
                ListFilesResponse getFoldersResponse = await _googleDriveService.GetFolders();
                if (getFoldersResponse != null)
                {
                    importFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.IMPORT)).Select(f => f.Id).FirstOrDefault();
                    if (!string.IsNullOrEmpty(importFolderId))
                    {
                        //Console.WriteLine($"importFolderId:{importFolderId}");
                        accountFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(accountName) && f.Parents.Contains(importFolderId)).Select(f => f.Id).FirstOrDefault();
                        if (!string.IsNullOrEmpty(accountFolderId))
                        {
                            //Console.WriteLine($"accountFolderId:{accountFolderId}");
                            imagesFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.IMAGES) && f.Parents.Contains(accountFolderId)).Select(f => f.Id).FirstOrDefault();
                            if (!string.IsNullOrEmpty(imagesFolderId))
                            {
                                //Console.WriteLine($"imagesFolderId:{imagesFolderId}");
                                newFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.NEW) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                doneFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.DONE) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                errorFolderId = getFoldersResponse.Files.Where(f => f.Name.Equals(DriveImportConstants.FolderNames.ERROR) && f.Parents.Contains(imagesFolderId)).Select(f => f.Id).FirstOrDefault();
                                //Console.WriteLine($"n:{newFolderId} d:{doneFolderId} e:{errorFolderId}");
                            }
                        }
                    }
                }
            }

            // If any essential folders are missing verify and create the folder structure.
            if (string.IsNullOrEmpty(newFolderId) || string.IsNullOrEmpty(doneFolderId) || string.IsNullOrEmpty(errorFolderId))
            {
                folderIds = null;
                _context.Vtex.Logger.Info("SheetImport", null, "Verifying folder structure.");
                Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

                if (folders == null)
                {
                    _context.Vtex.Logger.Warn("SheetImport", null, $"Error accessing Drive. {accountName}");
                    return ($"Error accessing Drive.");
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
                {
                    importFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                }
                else
                {
                    importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
                }

                if (string.IsNullOrEmpty(importFolderId))
                {
                    _context.Vtex.Logger.Warn("SheetImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                    return ($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
                }

                folders = await _googleDriveService.ListFolders(importFolderId);

                if (!folders.ContainsValue(accountName))
                {
                    accountFolderId = await _googleDriveService.CreateFolder(accountName, importFolderId);
                }
                else
                {
                    accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
                }

                if (string.IsNullOrEmpty(accountFolderId))
                {
                    _context.Vtex.Logger.Warn("SheetImport", null, $"Could not find {accountFolderId} folder");
                    return ($"Could not find {accountFolderId} folder");
                }

                folders = await _googleDriveService.ListFolders(accountFolderId);

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
                {
                    imagesFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                }
                else
                {
                    imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
                }

                if (string.IsNullOrEmpty(imagesFolderId))
                {
                    _context.Vtex.Logger.Warn("SheetImport", null, $"Could not find {imagesFolderId} folder");
                    return ($"Could not find {imagesFolderId} folder");
                }

                folders = await _googleDriveService.ListFolders(imagesFolderId);

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
                {
                    newFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
                    bool setPermission = await _googleDriveService.SetPermission(newFolderId);
                    if (!setPermission)
                    {
                        _context.Vtex.Logger.Error("SheetImport", "SetPermission", $"Could not set permissions on '{DriveImportConstants.FolderNames.NEW}' folder {newFolderId}");
                    }
                }
                else
                {
                    newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
                {
                    doneFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
                }
                else
                {
                    doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
                }

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
                {
                    errorFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
                }
                else
                {
                    errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;
                }
            }

            if (folderIds == null)
            {
                folderIds = new FolderIds
                {
                    AccountFolderId = accountFolderId,
                    DoneFolderId = doneFolderId,
                    ErrorFolderId = errorFolderId,
                    ImagesFolderId = imagesFolderId,
                    ImportFolderId = imagesFolderId,
                    NewFolderId = newFolderId
                };

                await _driveImportRepository.SaveFolderIds(folderIds, accountName);
            }

            stopWatch.Stop();
            _context.Vtex.Logger.Debug("SheetImport", null, $"Verifed folders {stopWatch.ElapsedMilliseconds}");

            Dictionary<string, string> images = new Dictionary<string, string>();
            //StringBuilder results = new StringBuilder("Sku,Name,Label,isMain,Status,Message");
            List<string> results = new List<string>();
            results.Add("Sku,Name,Label,isMain,Status,Message");

            // GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId);
            stopWatch.Restart();
            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            stopWatch.Stop();
            _context.Vtex.Logger.Debug("SheetImport", null, $"Getting images files {stopWatch.ElapsedMilliseconds}");
            stopWatch.Restart();
            ListFilesResponse spreadsheets = await _googleDriveService.ListSheetsInFolder(imagesFolderId);
            stopWatch.Stop();
            _context.Vtex.Logger.Debug("SheetImport", null, $"Getting spreadsheets {stopWatch.ElapsedMilliseconds}");
            if (imageFiles != null && spreadsheets != null)
            {
                //foreach(var sheet in spreadsheets.Files)
                //{
                //    Console.WriteLine($"spreadsheets.Files [{sheet.Id}]");
                //}

                var sheetIds = spreadsheets.Files.Select(s => s.Id);
                if (sheetIds != null)
                {
                    foreach (var sheetId in sheetIds)
                    {
                        Dictionary<string, int> headerIndexDictionary = new Dictionary<string, int>();
                        Dictionary<string, string> columns = new Dictionary<string, string>();
                        string sheetContent = await _googleDriveService.GetSheet(sheetId, string.Empty);
                        //_context.Vtex.Logger.Debug("SheetImport", null, $"[{sheetIds}] sheetContent: {sheetContent} ");

                        if (string.IsNullOrEmpty(sheetContent))
                        {
                            await ClearLockAfterDelay(5000);
                            return ("Empty Spreadsheet Response.");
                        }

                        GoogleSheet googleSheet = JsonConvert.DeserializeObject<GoogleSheet>(sheetContent);
                        string valueRange = googleSheet.ValueRanges[0].Range;
                        string sheetName = valueRange.Split("!")[0];
                        string[] sheetHeader = googleSheet.ValueRanges[0].Values[0];
                        int headerIndex = 0;
                        int rowCount = googleSheet.ValueRanges[0].Values.Count();
                        int writeBlockSize = Math.Max(rowCount / DriveImportConstants.WRITE_BLOCK_SIZE_DIVISOR, DriveImportConstants.MIN_WRITE_BLOCK_SIZE);
                        Console.WriteLine($"Write block size = {writeBlockSize}");
                        int offset = 0;
                        _context.Vtex.Logger.Debug("SheetImport", null, $"'{sheetName}' Row count: {rowCount} ");
                        foreach (string header in sheetHeader)
                        {
                            //Console.WriteLine($"({headerIndex}) sheetHeader = {header}");
                            headerIndexDictionary.Add(header.ToLower(), headerIndex);
                            headerIndex++;
                        }

                        int statusColumnNumber = headerIndexDictionary["status"] + 65;
                        string statusColumnLetter = ((char)statusColumnNumber).ToString();
                        int messageColumnNumber = headerIndexDictionary["message"] + 65;
                        string messageColumnLetter = ((char)messageColumnNumber).ToString();
                        int dateColumnNumber = headerIndexDictionary["date"] + 65;
                        string dateColumnLetter = ((char)dateColumnNumber).ToString();

                        //string[][] arrValuesToWrite = new string[rowCount][];
                        string[][] arrValuesToWrite = new string[writeBlockSize][];

                        for (int index = 1; index < rowCount; index++)
                        {
                            //stopWatch.Restart();
                            string identificatorType = string.Empty;
                            string id = string.Empty;
                            string imageName = string.Empty;
                            string imageLabel = string.Empty;
                            string main = string.Empty;
                            string skuContext = string.Empty;
                            string imageFileName = string.Empty;
                            string statusColumn = string.Empty;
                            string activateSkuValue = string.Empty;
                            bool processLine = true;

                            string[] dataValues = googleSheet.ValueRanges[0].Values[index];
                            if (headerIndexDictionary.ContainsKey("type") && headerIndexDictionary["type"] < dataValues.Count())
                                identificatorType = dataValues[headerIndexDictionary["type"]];
                            if (headerIndexDictionary.ContainsKey("value") && headerIndexDictionary["value"] < dataValues.Count())
                                id = dataValues[headerIndexDictionary["value"]];
                            if (headerIndexDictionary.ContainsKey("name") && headerIndexDictionary["name"] < dataValues.Count())
                                imageName = dataValues[headerIndexDictionary["name"]];
                            if (headerIndexDictionary.ContainsKey("main") && headerIndexDictionary["main"] < dataValues.Count())
                                main = dataValues[headerIndexDictionary["main"]];
                            if (headerIndexDictionary.ContainsKey("attributes") && headerIndexDictionary["attributes"] < dataValues.Count())
                                skuContext = dataValues[headerIndexDictionary["attributes"]];
                            if (headerIndexDictionary.ContainsKey("image") && headerIndexDictionary["image"] < dataValues.Count())
                                imageFileName = dataValues[headerIndexDictionary["image"]];
                            if (headerIndexDictionary.ContainsKey("label") && headerIndexDictionary["label"] < dataValues.Count())
                                imageLabel = dataValues[headerIndexDictionary["label"]];
                            if (headerIndexDictionary.ContainsKey("activate") && headerIndexDictionary["activate"] < dataValues.Count())
                                activateSkuValue = dataValues[headerIndexDictionary["activate"]];

                            if (headerIndexDictionary.ContainsKey("status") && headerIndexDictionary["status"] < dataValues.Count())
                                statusColumn = dataValues[headerIndexDictionary["status"]];

                            if (string.IsNullOrEmpty(identificatorType) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(imageFileName))
                            {
                                //Console.WriteLine($"Line ({index + 1}) is Empty!");
                                //Console.WriteLine($"arrValuesToWrite[{index - offset - 1}] = new string[]");
                                arrValuesToWrite[index - offset - 1] = new string[] { null, null, null };
                                processLine = false;
                            }

                            if (processLine && !string.IsNullOrWhiteSpace(statusColumn) && statusColumn.ToLower().Contains("done"))
                            {
                                //Console.WriteLine($"Line ({index + 1}) is Done! {identificatorType}:{id} {statusColumn}");
                                //Console.WriteLine($"arrValuesToWrite[{index - offset - 1}] = new string[]");
                                arrValuesToWrite[index - offset - 1] = new string[] { null, null, null };
                                processLine = false;
                            }

                            Console.WriteLine($"'{imageFileName}' [{imageFileName.Equals("DELETE")}] '{identificatorType}' ({id}) ");
                            if (processLine && !string.IsNullOrWhiteSpace(imageFileName) && imageFileName.Equals("DELETE") && !string.IsNullOrWhiteSpace(identificatorType))
                            {
                                bool deleted = await this.ProcessDelete(identificatorType, id, imageName);

                                string result = deleted ? "Done" : "Error";
                                string[] arrLineValuesToWrite = new string[] { result, null, $"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}" };
                                arrValuesToWrite[index - offset - 1] = arrLineValuesToWrite;
                                processLine = false;
                            }

                            //stopWatch.Stop();
                            //_context.Vtex.Logger.Debug("SheetImport", null, $"Read line {index} {stopWatch.ElapsedMilliseconds}");

                            if (processLine)
                            {
                                UpdateResponse updateResponse = null;
                                GoogleFile googleFile = imageFiles.Files.Where(i => i.Name.Equals(imageFileName)).FirstOrDefault();
                                if (googleFile != null)
                                {
                                    string fileNameForImport = string.Empty;
                                    if (string.IsNullOrEmpty(main))
                                    {
                                        fileNameForImport = $"{identificatorType},{id},{imageName},{imageLabel},";
                                    }
                                    else
                                    {
                                        fileNameForImport = $"{identificatorType},{id},{imageName},{imageLabel},Main";
                                    }

                                    if (!string.IsNullOrEmpty(skuContext))
                                    {
                                        fileNameForImport = $"{fileNameForImport},{skuContext}";
                                    }

                                    if (!string.IsNullOrEmpty(googleFile.FileExtension))
                                    {
                                        //Console.WriteLine($"FileExtension = {googleFile.FileExtension}");
                                        fileNameForImport = $"{fileNameForImport}.{googleFile.FileExtension}";
                                    }
                                    else
                                    {
                                        fileNameForImport = $"{fileNameForImport}.jpg";
                                    }

                                    Console.WriteLine($"Attempting to Process '({index}/{rowCount}) {fileNameForImport}' Link = {googleFile.WebContentLink}");
                                    stopWatch.Restart();
                                    updateResponse = await this.ProcessImageFile(fileNameForImport, googleFile.WebContentLink.ToString(), activateSkuValue);
                                    stopWatch.Stop();
                                    _context.Vtex.Logger.Debug("SheetImport", null, $"Process ({index}/{rowCount}) {stopWatch.ElapsedMilliseconds}");

                                    results.AddRange(updateResponse.Results);
                                    updated = updateResponse.Success;
                                    if (updated)
                                    {
                                        doneCount++;
                                        doneFileNames.Add(googleFile.Name);
                                        doneFileIds.Add(googleFile.Id);
                                    }
                                    else
                                    {
                                        if (!string.IsNullOrEmpty(updateResponse.StatusCode) && updateResponse.StatusCode.Equals(DriveImportConstants.GATEWAY_TIMEOUT))
                                        {
                                            _context.Vtex.Logger.Warn("SheetImport", null, $"Skipping ({index}) '{googleFile.Name}' {JsonConvert.SerializeObject(updateResponse)}");
                                        }
                                        else
                                        {
                                            errorCount++;
                                            errorFileNames.Add(googleFile.Name);
                                            errorFileIds.Add(googleFile.Id);
                                        }
                                    }

                                    Console.WriteLine($"UpdateResponse {updateResponse.Success} {updateResponse.Message}");
                                }
                                else
                                {
                                    updateResponse = new UpdateResponse
                                    {
                                        Message = $"Could not load file '{imageFileName}'"
                                    };
                                }

                                string result = updateResponse.Success ? "Done" : "Error";
                                string[] arrLineValuesToWrite = new string[] { result, updateResponse.Message, $"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}" };
                                //string[] arrLineValuesToWrite = new string[]
                                //{
                                //    result,
                                //    updateResponse.Results != null ? string.Join(", ", updateResponse.Results) : updateResponse.Message,
                                //    $"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}" 
                                //};

                                arrValuesToWrite[index - offset - 1] = arrLineValuesToWrite;
                            }

                            //Console.WriteLine($"WRITE TEST: {index} % {writeBlockSize} = {index % writeBlockSize}");
                            if (index % writeBlockSize == 0 || index + 1 == rowCount)
                            {
                                ValueRange valueRangeToWrite = new ValueRange
                                {
                                    Range = $"{sheetName}!{statusColumnLetter}{offset + 2}:{dateColumnLetter}{offset + writeBlockSize + 1}",
                                    Values = arrValuesToWrite
                                };

                                stopWatch.Restart();
                                var writeToSheetResult = await _googleDriveService.WriteSpreadsheetValues(sheetId, valueRangeToWrite);
                                stopWatch.Stop();
                                _context.Vtex.Logger.Debug("SheetImport", null, $"Writing to sheet {stopWatch.ElapsedMilliseconds} {JsonConvert.SerializeObject(writeToSheetResult)}");
                                offset += writeBlockSize;
                                arrValuesToWrite = new string[writeBlockSize][];
                                //Console.WriteLine($"offset = {offset}");
                            }
                        }
                    }
                }
                else
                {
                    await ClearLockAfterDelay(5000);
                    return ("No Spreadsheet Found!");
                }
            }

            if (doneCount + errorCount > 0)
            {
                foreach (string fileId in doneFileIds)
                {
                    bool moved = await _googleDriveService.MoveFile(fileId, doneFolderId);
                    if (!moved)
                    {
                        _context.Vtex.Logger.Error("SheetImport", "MoveFile", $"Failed to move Id '{fileId}' to folder '{DriveImportConstants.FolderNames.DONE}'");
                    }
                }

                foreach (string fileId in errorFileIds)
                {
                    bool moved = await _googleDriveService.MoveFile(fileId, errorFolderId);
                    if (!moved)
                    {
                        _context.Vtex.Logger.Error("SheetImport", "MoveFile", $"Failed to move Id '{fileId}' to folder '{DriveImportConstants.FolderNames.ERROR}'");
                    }
                }

                _context.Vtex.Logger.Info("SheetImport", null, $"Imported {doneCount} image(s).  {errorCount} image(s) not imported.  Done:[{string.Join(" ", doneFileNames)}] Error:[{string.Join(" ", errorFileNames)}]");
                _context.Vtex.Logger.Info("SheetImport", "Results", JsonConvert.SerializeObject(results));
            }

            if (errorCount > 0)
            {
                try
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (string textLine in results)
                    {
                        sb.AppendLine(textLine);
                    }

                    string fileId = await _googleDriveService.SaveFile(sb);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        await _googleDriveService.RenameFile(fileId, $"SheetErrors_{DateTime.Now.Date}");
                        await _googleDriveService.MoveFile(fileId, errorFolderId);
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("SheetImport", "Results", $"Error saving error list", ex);
                }
            }

            await ClearLockAfterDelay(5000);

            return ($"Imported {doneCount} image(s).  {errorCount} image(s) not imported.");
        }

        public async Task<UpdateResponse> UpdateSkuImage(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl)
        {
            //POST https://{{accountName}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/file
            //    {
            //                    "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //     "Url": "https://images2.alphacoders.com/509/509945.jpg"
            //    }

            bool success = false;
            string responseContent = string.Empty;
            string statusCode = string.Empty;

            if (string.IsNullOrEmpty(skuId) || string.IsNullOrEmpty(imageUrl))
            {
                responseContent = "Missing Parameter";
            }
            else
            {
                try
                {
                    ImageUpdate imageUpdate = new ImageUpdate
                    {
                        IsMain = isMain,
                        Label = imageLabel,
                        Name = imageName,
                        Text = null,
                        Url = imageUrl
                    };

                    string jsonSerializedData = JsonConvert.SerializeObject(imageUpdate);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                        Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    //request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);
                    //if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                    //{
                    //    //for (int cnt = 1; cnt < 2; cnt++)
                    //    //{
                    //    //await Task.Delay(cnt * 1000 * 10);
                    //    await Task.Delay(1000 * 20);
                    //    request = new HttpRequestMessage
                    //        {
                    //            Method = HttpMethod.Post,
                    //            RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                    //            Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    //        };

                    //        if (authToken != null)
                    //        {
                    //            request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    //            request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    //            request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    //        }

                    //        client = _clientFactory.CreateClient();
                    //        response = await client.SendAsync(request);
                    //    //_context.Vtex.Logger.Info("UpdateSkuImage", null, $"Sku {skuId} '{imageName}' retry ({cnt}) [{response.StatusCode}]");
                    //    _context.Vtex.Logger.Info("UpdateSkuImage", null, $"Sku {skuId} '{imageName}' retry  [{response.StatusCode}]");
                    //    //    if (response.IsSuccessStatusCode)
                    //    //    {
                    //    //        break;
                    //    //    }
                    //    //}
                    //}

                    responseContent = await response.Content.ReadAsStringAsync();
                    success = response.IsSuccessStatusCode;
                    if(!success)
                    {
                        _context.Vtex.Logger.Info("UpdateSkuImage", null, $"Response: {responseContent}  [{response.StatusCode}] for request '{jsonSerializedData}' to {request.RequestUri}");
                    }

                    statusCode = response.StatusCode.ToString();
                    if(string.IsNullOrEmpty(responseContent))
                    {
                        responseContent = $"Updated:{success} {response.StatusCode}";
                    }
                    else if(responseContent.Contains(DriveImportConstants.ARCHIVE_CREATED))
                    {
                        // If the image was already added to the sku, consider it a success
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("UpdateSkuImage", null, $"Error updating sku '{skuId}' {imageName}", ex);
                    success = false;
                    responseContent = ex.Message;
                }
            }

            UpdateResponse updateResponse = new UpdateResponse
            {
                Success = success,
                Message = responseContent,
                StatusCode = statusCode
            };

            return updateResponse;
        }

        public async Task<UpdateResponse> UpdateSkuImageArchive(string skuId, string imageName, string imageLabel, bool isMain, string imageId)
        {
            //POST https://{{accountname}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/archive/{{imageId}}
            //    {
            //     "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //    }

            bool success = false;
            string responseContent = string.Empty;

            if (string.IsNullOrEmpty(skuId) || string.IsNullOrEmpty(imageId))
            {
                responseContent = "Missing Parameter";
            }
            else
            {
                try
                {
                    ImageUpdate imageUpdate = new ImageUpdate
                    {
                        IsMain = isMain,
                        Label = imageLabel,
                        Name = imageName,
                        Text = null
                    };

                    string jsonSerializedData = JsonConvert.SerializeObject(imageUpdate);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/archive/{imageId}"),
                        Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    //request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                    success = response.IsSuccessStatusCode;
                    if (!success)
                    {
                        _context.Vtex.Logger.Info("UpdateSkuImageArchive", null, $"Response: {responseContent}  [{response.StatusCode}] for request '{jsonSerializedData}' to {request.RequestUri}");
                    }

                    if (string.IsNullOrEmpty(responseContent))
                    {
                        responseContent = $"Updated:{success} {response.StatusCode}";
                    }
                    else if (responseContent.Contains(DriveImportConstants.ARCHIVE_CREATED))
                    {
                        // If the image was already added to the sku, consider it a success
                        success = true;
                    }

                    _context.Vtex.Logger.Info("UpdateSkuImageArchive", null, $"Updating sku '{skuId}' '{imageName}' from archive '{imageId}' [{response.StatusCode}]");
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("UpdateSkuImageArchive", null, $"Error updating sku '{skuId}' {imageName}", ex);
                    success = false;
                    responseContent = ex.Message;
                }
            }

            UpdateResponse updateResponse = new UpdateResponse
            {
                Success = success,
                Message = responseContent
            };

            return updateResponse;
        }

        public async Task<bool> UpdateSkuImageByFormData(string skuId, string imageName, string imageLabel, bool isMain, byte[] imageStream)
        {
            //POST https://{{accountName}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/file
            //    {
            //                    "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //     "Url": "https://images2.alphacoders.com/509/509945.jpg"
            //    }

            bool success = false;

            try
            {
                MultipartFormDataContent form = new MultipartFormDataContent();
                if (isMain)
                {
                    form.Add(new ByteArrayContent(imageStream, 0, imageStream.Length), "main", imageName);
                }
                else
                {
                    form.Add(new ByteArrayContent(imageStream, 0, imageStream.Length), imageName, imageName);
                }

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file?an={this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.ACCEPT, DriveImportConstants.APPLICATION_JSON);
                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                //MerchantSettings merchantSettings = await _driveImportRepository.GetMerchantSettings();

                //request.Headers.Add(DriveImportConstants.APP_TOKEN, merchantSettings.AppToken);
                //request.Headers.Add(DriveImportConstants.APP_KEY, merchantSettings.AppKey);

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("UpdateSkuImageByFormData", null, $"Error updating sku '{skuId}' {imageName}", ex);
            }

            return success;
        }

        public async Task<string> GetSkuIdFromReference(string skuRefId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/sku/stockkeepingunitidbyrefid/{{refId}}

            string skuId = string.Empty;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/sku/stockkeepingunitidbyrefid/{skuRefId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    skuId = JsonConvert.DeserializeObject<String>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkuIdFromReference", null, $"Could not get sku for reference id '{skuRefId}'  [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkuIdFromReference", null, $"Error getting sku for reference id '{skuRefId}'", ex);
            }

            return skuId;
        }

        public async Task<string> GetProductIdFromReference(string productRefId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/products/productgetbyrefid/{{refId}}

            string productId = string.Empty;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/products/productgetbyrefid/{productRefId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    ProductResponse productResponse = JsonConvert.DeserializeObject<ProductResponse>(responseContent);
                    if (productResponse != null)
                    {
                        productId = productResponse.Id.ToString();
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetProductIdFromReference", null, $"Could not get product id for reference '{productRefId}' [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetProductIdFromReference", null, $"Error getting product id for reference '{productRefId}'", ex);
            }

            return productId;
        }

        public async Task<List<string>> GetSkusFromProductId(string productId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/sku/stockkeepingunitByProductId/{{productId}}

            List<string> skuIds = new List<string>();

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/sku/stockkeepingunitByProductId/{productId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    List<ProductSkusResponse> productSkusResponses = JsonConvert.DeserializeObject<List<ProductSkusResponse>>(responseContent);
                    foreach (ProductSkusResponse productSkusResponse in productSkusResponses)
                    {
                        string skuId = productSkusResponse.Id;
                        skuIds.Add(skuId);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkusFromProductId", null, $"Could not get skus for product id '{productId}'  [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkusFromProductId", null, $"Error getting skus for product id '{productId}'", ex);
            }

            return skuIds;
        }

        public async Task<GetSkuContextResponse> GetSkuContext(string skuId)
        {
            // GET https://{accountName}.{environment}.com.br/api/catalog_system/pvt/sku/stockkeepingunitbyid/skuId

            GetSkuContextResponse getSkuContextResponse = null;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/sku/stockkeepingunitbyid/{skuId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    getSkuContextResponse = JsonConvert.DeserializeObject<GetSkuContextResponse>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkuContext", null, $"Could not get sku for id '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkuContext", null, $"Error getting sku for id '{skuId}'", ex);
            }

            return getSkuContextResponse;
        }

        public async Task<UpdateResponse> UpdateSku(string skuId, UpdateSkuRequest updateSkuRequest)
        {
            //PUT https://{accountName}.{environment}.com.br/api/catalog/pvt/stockkeepingunit/skuId

            bool success = false;
            string responseContent = string.Empty;

            if (string.IsNullOrEmpty(skuId) || updateSkuRequest == null)
            {
                responseContent = "Missing Parameter";
            }
            else
            {
                try
                {
                    string jsonSerializedData = JsonConvert.SerializeObject(updateSkuRequest);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Put,
                        RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}"),
                        Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    //request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                    success = response.IsSuccessStatusCode;
                    if (!success)
                    {
                        _context.Vtex.Logger.Info("UpdateSku", null, $"Response: {responseContent}  [{response.StatusCode}] for request '{jsonSerializedData}' to {request.RequestUri}");
                    }

                    if (string.IsNullOrEmpty(responseContent))
                    {
                        responseContent = $"Updated:{success} {response.StatusCode}";
                    }

                    _context.Vtex.Logger.Info("UpdateSku", null, $"Updating sku '{skuId}' [{response.StatusCode}]");
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("UpdateSku", null, $"Error updating sku '{skuId}' ", ex);
                    success = false;
                    responseContent = ex.Message;
                }
            }

            UpdateResponse updateResponse = new UpdateResponse
            {
                Success = success,
                Message = responseContent
            };

            return updateResponse;
        }

        public async Task<bool> ActivateSku(string skuId, bool isActive)
        {
            bool success = false;
            Console.WriteLine($"    -   ActivateSku {skuId} to {isActive}");
            if (!string.IsNullOrEmpty(skuId))
            {
                GetSkuResponse getSkuResponse = await this.GetSku(skuId);
                if (getSkuResponse != null)
                {
                    if (getSkuResponse.IsActive.Equals(isActive))
                    {
                        _context.Vtex.Logger.Info("ActivateSku", null, $"Sku '{skuId}' active state is already {getSkuResponse.IsActive}.");
                        success = true;
                    }
                    else
                    {
                        UpdateSkuRequest updateSkuRequest = new UpdateSkuRequest
                        {
                            EstimatedDateArrival = getSkuResponse.EstimatedDateArrival,
                            IsActive = isActive,
                            KitItensSellApart = getSkuResponse.KitItensSellApart,
                            CommercialConditionId = getSkuResponse.CommercialConditionId,
                            CreationDate = getSkuResponse.CreationDate,
                            CubicWeight = getSkuResponse.CubicWeight,
                            Height = getSkuResponse.Height,
                            Id = getSkuResponse.Id,
                            IsKit = getSkuResponse.IsKit,
                            Length = getSkuResponse.Length,
                            ManufacturerCode = getSkuResponse.ManufacturerCode,
                            MeasurementUnit = getSkuResponse.MeasurementUnit,
                            ModalType = getSkuResponse.ModalType,
                            Name = getSkuResponse.Name,
                            PackagedHeight = getSkuResponse.PackagedHeight,
                            PackagedLength = getSkuResponse.PackagedLength,
                            PackagedWeightKg = getSkuResponse.PackagedWeightKg,
                            PackagedWidth = getSkuResponse.PackagedWidth,
                            ProductId = getSkuResponse.ProductId,
                            RefId = getSkuResponse.RefId,
                            RewardValue = getSkuResponse.RewardValue,
                            UnitMultiplier = getSkuResponse.UnitMultiplier,
                            WeightKg = getSkuResponse.WeightKg,
                            Width = getSkuResponse.Width
                        };

                        UpdateResponse updateResponse = await this.UpdateSku(skuId, updateSkuRequest);
                        success = updateResponse.Success;
                        _context.Vtex.Logger.Info("ActivateSku", null, $"Sku '{skuId}' active state set to '{isActive}'? {updateResponse.Success} '{updateResponse.Message}'");
                    }
                }
            }

            return success;
        }

        public async Task<UpdateResponse> ProcessImageFile(string fileName, string webLink, string activateSkuValue = null)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            UpdateResponse updateResponse = new UpdateResponse();
            List<string> messages = new List<string>();
            bool success = false;
            string identificatorType = string.Empty;
            string id = string.Empty;
            string imageName = string.Empty;
            string imageLabel = string.Empty;
            bool isMain = false;
            string skuContext = string.Empty;
            string skuContextField = string.Empty;
            string skuContextValue = string.Empty;
            List<string> resultsList = new List<string>();
            bool skuIsActive = true;
            bool activateSku = false;
            if (!string.IsNullOrEmpty(activateSkuValue))
            {
                activateSku = bool.TryParse(activateSkuValue, out skuIsActive);
            }

            // IdentificatorType, Id, ImageName, ImageLabel, Main
            string[] fileNameArr = fileName.Split('.');
            if (fileNameArr.Count() == 2 && !string.IsNullOrEmpty(fileNameArr[0]))
            {
                string[] fileNameParsed = fileNameArr[0].Split(',');
                if ((fileNameParsed.Count() <= 6 && fileNameParsed.Count() >= 4))
                {
                    identificatorType = fileNameParsed[0];
                    id = fileNameParsed[1];
                    imageName = fileNameParsed[2];
                    imageLabel = fileNameParsed[3];
                    if (fileNameParsed.Count() >= 5)
                    {
                        isMain = fileNameParsed[4].Equals("Main", StringComparison.CurrentCultureIgnoreCase);
                    }

                    if (fileNameParsed.Count() >= 6)
                    {
                        skuContext = fileNameParsed[5];
                        string[] skuContextArr = skuContext.Split("=");
                        skuContextField = skuContextArr[0];
                        skuContextValue = skuContextArr[1];
                    }

                    string parsedFilename = $"[Type:{identificatorType} Id:{id} Name:{imageName} Label:{imageLabel} Main? {isMain}]";
                    long? imageId = null;

                    switch (identificatorType)
                    {
                        case DriveImportConstants.IdentificatorType.SKU_ID:
                            stopWatch.Restart();
                            updateResponse = await this.UpdateSkuImage(id, imageName, imageLabel, isMain, webLink);
                            stopWatch.Stop();
                            _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {id} {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");

                            success = updateResponse.Success;
                            if (!updateResponse.Success)
                            {
                                messages.Add(updateResponse.Message);
                                string resultLine = $"{id},{imageName},{imageLabel},{isMain},{updateResponse.StatusCode},{updateResponse.Message}";
                                resultsList.Add(resultLine);
                            }
                            else if(activateSku)
                            {
                                this.ActivateSku(id, skuIsActive);
                            }

                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                            //resultsList.AppendLine($"{identificatorType},{id},{imageName},{imageLabel},{isMain},{updateResponse.Success},{updateResponse.StatusCode}");
                            break;
                        case DriveImportConstants.IdentificatorType.SKU_REF_ID:
                            string skuId = await this.GetSkuIdFromReference(id);
                            if (!string.IsNullOrEmpty(skuId))
                            {
                                stopWatch.Restart();
                                updateResponse = await this.UpdateSkuImage(skuId, imageName, imageLabel, isMain, webLink);
                                stopWatch.Stop();
                                _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {skuId} for Ref {id} {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                success = updateResponse.Success;
                                if (!updateResponse.Success)
                                {
                                    messages.Add(updateResponse.Message);
                                    string resultLine = $"{skuId},{imageName},{imageLabel},{isMain},{updateResponse.StatusCode},{updateResponse.Message}";
                                    resultsList.Add(resultLine);
                                }
                                else if (activateSku)
                                {
                                    this.ActivateSku(skuId, skuIsActive);
                                }
                            }
                            else
                            {
                                success = false;
                                messages.Add($"Failed to find sku id from reference {id}");
                                string resultLine = $",{imageName},{imageLabel},{isMain},,Failed to find sku id from reference {id}";
                                resultsList.Add(resultLine);
                            }

                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {skuId} from {identificatorType} {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                            //resultsList.AppendLine($"{identificatorType},{id},{imageName},{imageLabel},{isMain},{updateResponse.Success},{updateResponse.StatusCode}");
                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_REF_ID:
                            string prodId = await this.GetProductIdFromReference(id);
                            if (!string.IsNullOrEmpty(prodId))
                            {
                                List<string> prodRefSkuIds = await this.GetSkusFromProductId(prodId);
                                if (prodRefSkuIds != null && prodRefSkuIds.Count > 0)
                                {
                                    success = true;
                                    foreach (string prodRefSku in prodRefSkuIds)
                                    {
                                        bool proceed = true;
                                        if (!string.IsNullOrEmpty(skuContextField) && !string.IsNullOrEmpty(skuContextValue))
                                        {
                                            GetSkuContextResponse skuContextResponse = await this.GetSkuContext(prodRefSku);
                                            if (skuContextResponse != null && skuContextResponse.SkuSpecifications != null)
                                            {
                                                var skuSpecifications = skuContextResponse.SkuSpecifications.Where(s => s.FieldName.Equals(skuContextField, StringComparison.InvariantCultureIgnoreCase));

                                                // debug
                                                //List<string> specs = new List<string>();
                                                //foreach (Specification specification in skuContextResponse.SkuSpecifications)
                                                //{
                                                //    specs.Add($"1) {specification.FieldName} = {string.Join(",", specification.FieldValues)}");
                                                //}
                                                //foreach (Specification specification in skuSpecifications)
                                                //{
                                                //    specs.Add($"2) {specification.FieldName} = {string.Join(",", specification.FieldValues)}");
                                                //}
                                                //_context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, string.Join(":", specs));
                                                // end debug

                                                //proceed = skuSpecifications.Any(skuContextField => skuContextField.Equals(skuContextValue, StringComparison.InvariantCultureIgnoreCase));
                                                proceed = skuSpecifications.Any(s => s.FieldValues.Any(v => v.Equals(skuContextValue, StringComparison.InvariantCultureIgnoreCase)));
                                                if(proceed)
                                                {
                                                    _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Found match '{skuContextField}'='{skuContextValue}' for Sku {prodRefSku}");
                                                }
                                            }
                                            else
                                            {
                                                _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Failed to get SkuContext for Sku {prodRefSku}");
                                            }
                                        }

                                        //Console.WriteLine($"imageId='{imageId}' prodRefSku={prodRefSku}");
                                        if (proceed)
                                        {
                                            if (imageId != null)
                                            {
                                                stopWatch.Restart();
                                                updateResponse = await this.UpdateSkuImageArchive(prodRefSku, imageName, imageLabel, isMain, imageId.ToString());
                                                stopWatch.Stop();
                                                _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {prodRefSku} for ProdRef {id} from archive {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                                if (!updateResponse.Success)
                                                {
                                                    imageId = null;
                                                    stopWatch.Restart();
                                                    updateResponse = await this.UpdateSkuImage(prodRefSku, imageName, imageLabel, isMain, webLink);
                                                    stopWatch.Stop();
                                                    _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {prodRefSku} for ProdRef {id} after failing to load from archive {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                                }
                                            }
                                            else
                                            {
                                                stopWatch.Restart();
                                                updateResponse = await this.UpdateSkuImage(prodRefSku, imageName, imageLabel, isMain, webLink);
                                                stopWatch.Stop();
                                                _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {prodRefSku} for ProdRef {id} {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                            }

                                            success &= updateResponse.Success;
                                            if (!updateResponse.Success)
                                            {
                                                messages.Add(updateResponse.Message);
                                                string resultLine = $"{prodRefSku},{imageName},{imageLabel},{isMain},{updateResponse.StatusCode},{updateResponse.Message}";
                                                resultsList.Add(resultLine);
                                            }
                                            else if (imageId == null && !updateResponse.Message.Contains(DriveImportConstants.ARCHIVE_CREATED))
                                            {
                                                try
                                                {
                                                    SkuUpdateResponse skuUpdateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(updateResponse.Message);
                                                    imageId = await this.GetArchiveId(skuUpdateResponse, imageLabel);
                                                    _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"Sku {prodRefSku} Image Id: {imageId}");
                                                }
                                                catch (Exception ex)
                                                {
                                                    _context.Vtex.Logger.Error("ProcessImageFile", parsedFilename, $"Error parsing SkuUpdateResponse {updateResponse.Message} [{updateResponse.StatusCode}]", ex);
                                                }
                                            }

                                            if (updateResponse.Success && activateSku)
                                            {
                                                this.ActivateSku(prodRefSku, skuIsActive);
                                            }

                                            //messages.Add($"{prodRefSku}: {updateResponse.Success}");

                                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {prodRefSku} from {identificatorType} {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                                        }
                                        else
                                        {
                                            _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Did not match '{skuContextField}'='{skuContextValue}' for Sku {prodRefSku}");
                                        }
                                    }
                                }
                                else
                                {
                                    success = false;
                                    messages.Add($"Failed to find skus for product id {prodId}");
                                    string resultLine = $",{imageName},{imageLabel},{isMain},,Failed to find skus for product id {prodId}";
                                    resultsList.Add(resultLine);
                                }
                            }
                            else
                            {
                                success = false;
                                messages.Add($"Failed to find product id from reference {id}");
                                string resultLine = $",{imageName},{imageLabel},{isMain},,Failed to find product id from reference {id}";
                                resultsList.Add(resultLine);
                            }

                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_ID:
                            List<string> skuIds = await this.GetSkusFromProductId(id);
                            success = true;
                            foreach (string sku in skuIds)
                            {
                                bool proceed = true;
                                if (!string.IsNullOrEmpty(skuContextField) && !string.IsNullOrEmpty(skuContextValue))
                                {
                                    GetSkuContextResponse skuContextResponse = await this.GetSkuContext(sku);
                                    if (skuContextResponse != null && skuContextResponse.SkuSpecifications != null)
                                    {
                                        var skuSpecifications = skuContextResponse.SkuSpecifications.Where(s => s.FieldName.Equals(skuContextField, StringComparison.InvariantCultureIgnoreCase));
                                        proceed = skuSpecifications.Any(s => s.FieldValues.Any(v => v.Equals(skuContextValue, StringComparison.InvariantCultureIgnoreCase)));
                                        if (proceed)
                                        {
                                            _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Found match '{skuContextField}'='{skuContextValue}' for Sku {sku}");
                                        }
                                    }
                                    else
                                    {
                                        _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Failed to get SkuContext for Sku {sku}");
                                    }
                                }

                                if (proceed)
                                {
                                    if (imageId != null)
                                    {
                                        stopWatch.Restart();
                                        updateResponse = await this.UpdateSkuImageArchive(sku, imageName, imageLabel, isMain, imageId.ToString());
                                        stopWatch.Stop();
                                        _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {sku} for ProdId {id} from archive {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                        if (!updateResponse.Success)
                                        {
                                            imageId = null;
                                            stopWatch.Restart();
                                            updateResponse = await this.UpdateSkuImage(sku, imageName, imageLabel, isMain, webLink);
                                            stopWatch.Stop();
                                            _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {sku} for ProdId {id} after failing to load from archive {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                        }
                                    }
                                    else
                                    {
                                        stopWatch.Restart();
                                        updateResponse = await this.UpdateSkuImage(sku, imageName, imageLabel, isMain, webLink);
                                        stopWatch.Stop();
                                        _context.Vtex.Logger.Debug("ProcessImageFile", null, $"Update sku {sku} for ProdId {id}  {stopWatch.ElapsedMilliseconds} {updateResponse.Message}");
                                    }

                                    success &= updateResponse.Success;
                                    if (!updateResponse.Success)
                                    {
                                        messages.Add(updateResponse.Message);
                                        string resultLine = $"{sku},{imageName},{imageLabel},{isMain},{updateResponse.StatusCode},{updateResponse.Message}";
                                        resultsList.Add(resultLine);
                                    }
                                    else if (imageId == null && !updateResponse.Message.Contains(DriveImportConstants.ARCHIVE_CREATED))
                                    {
                                        try
                                        {
                                            SkuUpdateResponse skuUpdateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(updateResponse.Message);
                                            imageId = await this.GetArchiveId(skuUpdateResponse, imageLabel);
                                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"Sku {sku} Image Id: {imageId}");
                                        }
                                        catch (Exception ex)
                                        {
                                            _context.Vtex.Logger.Error("ProcessImageFile", parsedFilename, $"Error parsing SkuUpdateResponse {updateResponse.Message}", ex);
                                        }
                                    }

                                    //messages.Add($"{sku}:{updateResponse.Success}");
                                    if (updateResponse.Success && activateSku)
                                    {
                                        this.ActivateSku(sku, skuIsActive);
                                    }

                                    _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {sku} from {identificatorType} {id} success? {updateResponse.Success} '{updateResponse.Message}'");
                                }
                                else
                                {
                                    _context.Vtex.Logger.Debug("ProcessImageFile", parsedFilename, $"Did not match '{skuContextField}'='{skuContextValue}' for Sku {sku}");
                                }
                            }

                            break;
                        default:
                            messages.Add($"Type {identificatorType} not recognized.");
                            _context.Vtex.Logger.Warn("ProcessImageFile", parsedFilename, $"Type '{identificatorType}' is not recognized.  Filename '{fileName}'");
                            break;
                    }
                }
                else
                {
                    messages.Add("Invalid filename format");
                }
            }
            else
            {
                messages.Add("Invalid filename");
            }

            updateResponse.Success = success;
            updateResponse.Message = string.Join("-", messages);
            updateResponse.Results = resultsList;

            if (resultsList != null && resultsList.Count > 0)
            {
                _context.Vtex.Logger.Info("ProcessImageFile", "resultsList", JsonConvert.SerializeObject(resultsList));
            }

            return updateResponse;
        }

        private async Task<long?> GetArchiveId(SkuUpdateResponse skuUpdateResponse, string imageName)
        {
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            long? archiveId = null;
            try
            {
                for (int i = 1; i < 5; i++)
                {
                    GetSkuContextResponse getSkuContextResponse = await this.GetSkuContext(skuUpdateResponse.SkuId.ToString());
                    if (getSkuContextResponse != null)
                    {
                        foreach (Image image in getSkuContextResponse.Images)
                        {
                            Console.WriteLine($"GetSkuContextResponse '{image.ImageName}'='{imageName}' [{image.FileId}]");
                            if (image.ImageName != null && image.ImageName.Equals(imageName))
                            {
                                archiveId = image.FileId;
                                break;
                            }
                        }
                    }

                    if (archiveId != null)
                    {
                        break;
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
            }
            catch(Exception ex)
            {
                _context.Vtex.Logger.Error("GetArchiveId", null, $"Error getting archive id for sku {skuUpdateResponse.SkuId} '{imageName}'", ex);
            }

            stopWatch.Stop();
            _context.Vtex.Logger.Info("GetArchiveId", null, $"FileId: '{archiveId}' for '{imageName}' (sku:{skuUpdateResponse.SkuId} id:{skuUpdateResponse.Id})");
            _context.Vtex.Logger.Debug("GetArchiveId", null, $"Found archive id {archiveId != null}  {stopWatch.ElapsedMilliseconds}");

            return archiveId;
        }

        public async Task<bool> DeleteImageByName(string skuId, string imageName)
        {
            bool success = true;
            try
            {
                for (int i = 1; i < 5; i++)
                {
                    GetSkuImagesResponse[] getSkuResponse = await this.GetSkuImages(skuId);
                    if (getSkuResponse != null)
                    {
                        foreach (GetSkuImagesResponse skuResponse in getSkuResponse)
                        {
                            Console.WriteLine($"DeleteImage '{skuResponse.Name}'='{imageName}' [{skuResponse.Id}]");
                            if (skuResponse.Name != null && skuResponse.Name.Equals(imageName))
                            {
                                success &= await this.DeleteSkuImageByFileId(skuId, skuResponse.Id.ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("DeleteImage", null, $"Error deleting '{imageName}' for skuid {skuId}", ex);
            }

            _context.Vtex.Logger.Info("DeleteImage", null, $"Deleted FileId: '{skuId}' for '{imageName}'");

            return success;
        }

        public async Task<bool> ProcessDelete(string identificatorType, string id, string imageName)
        {
            bool success = true;

            switch (identificatorType)
            {
                case DriveImportConstants.IdentificatorType.SKU_ID:
                    if (!string.IsNullOrEmpty(imageName))
                    {
                        success = await this.DeleteImageByName(id, imageName);
                    }
                    else
                    {
                        success = await this.DeleteSkuImages(id);
                    }

                    break;
                case DriveImportConstants.IdentificatorType.SKU_REF_ID:
                    string skuId = await this.GetSkuIdFromReference(id);
                    if (!string.IsNullOrEmpty(skuId))
                    {
                        if (!string.IsNullOrEmpty(imageName))
                        {
                            success = await this.DeleteImageByName(skuId, imageName);
                        }
                        else
                        {
                            success = await this.DeleteSkuImages(skuId);
                        }
                    }
                    
                    break;
                case DriveImportConstants.IdentificatorType.PRODUCT_REF_ID:
                    string prodId = await this.GetProductIdFromReference(id);
                    if (!string.IsNullOrEmpty(prodId))
                    {
                        List<string> prodRefSkuIds = await this.GetSkusFromProductId(prodId);
                        if (prodRefSkuIds != null && prodRefSkuIds.Count > 0)
                        {
                            success = true;
                            foreach (string prodRefSku in prodRefSkuIds)
                            {
                                if (!string.IsNullOrEmpty(imageName))
                                {
                                    success &= await this.DeleteImageByName(prodRefSku, imageName);
                                }
                                else
                                {
                                    success &= await this.DeleteSkuImages(prodRefSku);
                                }
                            }
                        }
                    }

                    break;
                case DriveImportConstants.IdentificatorType.PRODUCT_ID:
                    List<string> skuIds = await this.GetSkusFromProductId(id);
                    success = true;
                    foreach (string sku in skuIds)
                    {
                        if (!string.IsNullOrEmpty(imageName))
                        {
                            success &= await this.DeleteImageByName(sku, imageName);
                        }
                        else
                        {
                            success &= await this.DeleteSkuImages(sku);
                        }
                    }

                    break;
            }

            return success;
        }

        private async Task<SkuUpdateResponse> ParseSkuUpdateResponse(string responseContent)
        {
            SkuUpdateResponse updateResponse = null;
            try
            {
                updateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(responseContent);
            }
            catch(Exception ex)
            {
                _context.Vtex.Logger.Error("ParseSkuUpdateResponse", null, $"Error parsing '{responseContent}'", ex);
            }

            return updateResponse;
        }

        public async Task<bool> DeleteSkuImageByFileId(string skuId, string skuFileId)
        {
            // DELETE https://{accountName}.{environment}.com.br/api/catalog/pvt/stockkeepingunit/skuId/file/skuFileId

            bool deleted = false;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file/{skuFileId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"DeleteSkuImageByFileId [{response.StatusCode}] {responseContent}");
                if (response.IsSuccessStatusCode)
                {
                    deleted = true;
                }
                else
                {
                    _context.Vtex.Logger.Warn("DeleteImage", null, $"Did not delete image '{skuFileId}' for skuid '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("DeleteImage", null, $"Error deleting image '{skuFileId}' for skuid '{skuId}'", ex);
            }

            return deleted;
        }

        public async Task<bool> DeleteSkuImages(string skuId)
        {
            // DELETE https://{accountName}.{environment}.com.br/api/catalog/pvt/stockkeepingunit/skuId/file

            bool deleted = false;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    deleted = true;
                }
                else
                {
                    _context.Vtex.Logger.Warn("DeleteSkuImages", null, $"Did not delete images for skuid '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("DeleteSkuImages", null, $"Error deleting images for skuid '{skuId}'", ex);
            }

            return deleted;
        }

        public async Task<GetSkuImagesResponse[]> GetSkuImages(string skuId)
        {
            // GET https://{accountName}.{environment}.com.br/api/catalog/pvt/stockkeepingunit/skuId/file

            GetSkuImagesResponse[] getSkuResponse = null;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    getSkuResponse = JsonConvert.DeserializeObject<GetSkuImagesResponse[]>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkuImages", null, $"Did not get images for skuid '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkuImages", null, $"Error getting images for skuid '{skuId}'", ex);
            }

            return getSkuResponse;
        }

        public async Task<GetSkuResponse> GetSku(string skuId)
        {
            // GET https://{accountName}.{environment}.com.br/api/catalog/pvt/stockkeepingunit/skuId

            GetSkuResponse getSkuResponse = null;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    getSkuResponse = JsonConvert.DeserializeObject<GetSkuResponse>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSku", null, $"Did not get images for skuid '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSku", null, $"Error getting images for skuid '{skuId}'", ex);
            }

            return getSkuResponse;
        }

        public async Task ClearLockAfterDelay(int delayInMilliseconds)
        {
            await Task.Delay(delayInMilliseconds);
            try
            {
                await _driveImportRepository.ClearImportLock();
                _context.Vtex.Logger.Info("DriveImport", null, $"Cleared lock: {DateTime.Now}");
                Console.WriteLine("Cleared lock");
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("DriveImport", null, "Failed to clear lock", ex);
            }
        }
    }
}
