namespace DriveImport.Controllers
{
    using DriveImport.Data;
    using DriveImport.Models;
    using DriveImport.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.TagHelpers;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using Vtex.Api.Context;

    public class RoutesController : Controller
    {
        private readonly IIOServiceContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IGoogleDriveService _googleDriveService;
        private readonly IVtexAPIService _vtexAPIService;
        private readonly IDriveImportRepository _driveImportRepository;

        public RoutesController(IIOServiceContext context, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory, IGoogleDriveService googleDriveService, IVtexAPIService vtexAPIService, IDriveImportRepository driveImportRepository)
        {
            this._context = context ?? throw new ArgumentNullException(nameof(context));
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            this._clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            this._googleDriveService = googleDriveService ?? throw new ArgumentNullException(nameof(googleDriveService));
            this._vtexAPIService = vtexAPIService ?? throw new ArgumentNullException(nameof(vtexAPIService));
            this._driveImportRepository = driveImportRepository ?? throw new ArgumentNullException(nameof(driveImportRepository));
        }

        public async Task<IActionResult> DriveImport()
        {
            Response.Headers.Add("Cache-Control", "no-cache");

            // check lock
            DateTime importStarted = await _driveImportRepository.CheckImportLock();

            Console.WriteLine($"Check lock: {importStarted}");

            TimeSpan elapsedTime = DateTime.Now - importStarted;
            if (elapsedTime.TotalHours < 2)
            {
                Console.WriteLine("Blocked by lock");
                _context.Vtex.Logger.Info("DriveImport", null, $"Blocked by lock.  Import started: {importStarted}");
                return Json($"Import started {importStarted} in progress.");
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
                    return Json($"Error accessing Drive.");
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
                    return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
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
                    return Json($"Could not find {accountFolderId} folder");
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
                    return Json($"Could not find {imagesFolderId} folder");
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

            if(folderIds == null)
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
                                UpdateResponse updateResponse = await _vtexAPIService.ProcessImageFile(file.Name, file.WebContentLink.ToString());
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

                        if(thereAreFiles && didSkip)
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

            if(errorCount > 0)
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
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("DriveImport", "Results", $"Error saving error list", ex);
                }
            }

            await ClearLockAfterDelay(5000);

            return Json($"Imported {doneCount} image(s).  {errorCount} image(s) not imported.");
        }

        public async Task<IActionResult> SheetImport()
        {
            Response.Headers.Add("Cache-Control", "no-cache");

            // check lock
            DateTime importStarted = await _driveImportRepository.CheckImportLock();

            //Console.WriteLine($"Check lock: {importStarted}");

            TimeSpan elapsedTime = DateTime.Now - importStarted;
            if (elapsedTime.TotalHours < 2)
            {
                Console.WriteLine("Blocked by lock");
                _context.Vtex.Logger.Info("SheetImport", null, $"Blocked by lock.  Import started: {importStarted}");
                return Json($"Import started {importStarted} in progress.");
            }

            await _driveImportRepository.SetImportLock(DateTime.Now);
            //Console.WriteLine($"Set new lock: {DateTime.Now}");
            _context.Vtex.Logger.Info("SheetImport", null, $"Set new lock: {DateTime.Now}");

            Console.WriteLine("Import from Spreadsheet started");
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
                    return Json($"Error accessing Drive.");
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
                    _context.Vtex.Logger.Info("SheetImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                    return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
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
                    _context.Vtex.Logger.Info("SheetImport", null, $"Could not find {accountFolderId} folder");
                    return Json($"Could not find {accountFolderId} folder");
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
                    _context.Vtex.Logger.Info("SheetImport", null, $"Could not find {imagesFolderId} folder");
                    return Json($"Could not find {imagesFolderId} folder");
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

            Dictionary<string, string> images = new Dictionary<string, string>();
            //StringBuilder results = new StringBuilder("Sku,Name,Label,isMain,Status,Message");
            List<string> results = new List<string>();
            results.Add("Sku,Name,Label,isMain,Status,Message");

            // GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId);

            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            ListFilesResponse spreadsheets = await _googleDriveService.ListSheetsInFolder(imagesFolderId);
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
                            return Json("Empty Spreadsheet Response.");
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
                        _context.Vtex.Logger.Info("SheetImport", null, $"'{sheetName}' Row count: {rowCount} ");
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
                            string identificatorType = string.Empty;
                            string id = string.Empty;
                            string imageName = string.Empty;
                            string imageLabel = string.Empty;
                            string main = string.Empty;
                            string skuContext = string.Empty;
                            string imageFileName = string.Empty;
                            string statusColumn = string.Empty;
                            bool processLine = true;
                            //foreach (string value in googleSheet.ValueRanges[0].Values[dataLine])
                            //{

                            //}

                            string[] dataValues = googleSheet.ValueRanges[0].Values[index];
                            if (headerIndexDictionary.ContainsKey("type") && headerIndexDictionary["type"] < dataValues.Count())
                                identificatorType = dataValues[headerIndexDictionary["type"]];
                            if (headerIndexDictionary.ContainsKey("value") && headerIndexDictionary["value"] < dataValues.Count())
                                id = dataValues[headerIndexDictionary["value"]];
                            if (headerIndexDictionary.ContainsKey("name") && headerIndexDictionary["name"] < dataValues.Count())
                                imageName = dataValues[headerIndexDictionary["name"]];
                            if (headerIndexDictionary.ContainsKey("main") && headerIndexDictionary["main"] < dataValues.Count())
                                main = dataValues[headerIndexDictionary["main"]];
                            if (headerIndexDictionary.ContainsKey("skucontext") && headerIndexDictionary["skucontext"] < dataValues.Count())
                                skuContext = dataValues[headerIndexDictionary["skucontext"]];
                            if (headerIndexDictionary.ContainsKey("image") && headerIndexDictionary["image"] < dataValues.Count())
                                imageFileName = dataValues[headerIndexDictionary["image"]];

                            if (headerIndexDictionary.ContainsKey("status") && headerIndexDictionary["status"] < dataValues.Count())
                                statusColumn = dataValues[headerIndexDictionary["status"]];

                            if (string.IsNullOrEmpty(identificatorType) || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(imageFileName))
                            {
                                //Console.WriteLine($"Line ({index + 1}) is Empty!");
                                //Console.WriteLine($"arrValuesToWrite[{index - offset - 1}] = new string[]");
                                arrValuesToWrite[index - offset - 1] = new string[] { null, null, null };
                                processLine = false;
                            }

                            if (!string.IsNullOrWhiteSpace(statusColumn) && statusColumn.ToLower().Contains("done"))
                            {
                                //Console.WriteLine($"Line ({index + 1}) is Done! {identificatorType}:{id} {statusColumn}");
                                //Console.WriteLine($"arrValuesToWrite[{index - offset - 1}] = new string[]");
                                arrValuesToWrite[index - offset - 1] = new string[] { null, null, null };
                                processLine = false;
                            }

                            if (processLine)
                            {
                                UpdateResponse updateResponse = null;
                                GoogleFile googleFile = imageFiles.Files.Where(i => i.Name.Equals(imageFileName)).FirstOrDefault();
                                if (googleFile != null)
                                {
                                    string fileNameForImport = string.Empty;
                                    if (string.IsNullOrEmpty(main))
                                    {
                                        fileNameForImport = $"{identificatorType},{id},{imageName},";
                                    }
                                    else
                                    {
                                        fileNameForImport = $"{identificatorType},{id},{imageName},Main";
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

                                    updateResponse = await _vtexAPIService.ProcessImageFile(fileNameForImport, googleFile.WebContentLink.ToString());

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
                                            _context.Vtex.Logger.Warn("SheetImport", null, $"Skipping {googleFile.Name} {JsonConvert.SerializeObject(updateResponse)}");
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
                            if(index % writeBlockSize == 0 || index + 1 == rowCount)
                            {
                                ValueRange valueRangeToWrite = new ValueRange
                                {
                                    Range = $"{sheetName}!{statusColumnLetter}{offset + 2}:{dateColumnLetter}{offset + writeBlockSize + 1}",
                                    Values = arrValuesToWrite
                                };

                                var writeToSheetResult = await _googleDriveService.WriteSpreadsheetValues(sheetId, valueRangeToWrite);
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
                    return Json("No Spreadsheet Found!");
                }
            }

            if (doneCount + errorCount > 0)
            {
                foreach(string fileId in doneFileIds)
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

            return Json($"Imported {doneCount} image(s).  {errorCount} image(s) not imported.");
        }

        public async Task<IActionResult> ProcessReturnUrl()
        {
            string code = _httpContextAccessor.HttpContext.Request.Query["code"];
            string siteUrl = _httpContextAccessor.HttpContext.Request.Query["state"];

            _context.Vtex.Logger.Info("ProcessReturnUrl", null, $"site=[{siteUrl}]");

            if (string.IsNullOrEmpty(siteUrl))
            {
                return BadRequest();
            }
            else
            {
                string redirectUri = $"https://{siteUrl}/{DriveImportConstants.APP_NAME}/{DriveImportConstants.REDIRECT_PATH}-code/?code={code}";
                return Redirect(redirectUri);
            }
        }

        public async Task<IActionResult> ProcessReturnCode()
        {
            bool success = false;
            bool watch = false;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            string importFolderId;
            string accountFolderId;
            string imagesFolderId;
            string newFolderId;
            string doneFolderId;
            string errorFolderId;
            string siteUrl = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.FORWARDED_HOST];
            string code = _httpContextAccessor.HttpContext.Request.Query["code"];

            _context.Vtex.Logger.Info("ProcessReturnCode", null, $"code=[{code}]");

            if (string.IsNullOrEmpty(code))
            {
                _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Missing return code. [{code}]");
                return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}&message=Missing return code.");
            }

            success = await _googleDriveService.ProcessReturn(code);

            if (!success)
            {
                _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Could not process code. [{code}]");
                return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}&message=Could not process code.");
            }

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Folder Ids loaded from storage. [{code}]");
                importFolderId = folderIds.ImagesFolderId;
                accountFolderId = folderIds.AccountFolderId;
                imagesFolderId = folderIds.ImagesFolderId;
                newFolderId = folderIds.NewFolderId;
                doneFolderId = folderIds.DoneFolderId;
                errorFolderId = folderIds.ErrorFolderId;
            }
            else
            {
                Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

                if (folders == null)
                {
                    await Task.Delay(500);
                    folders = await _googleDriveService.ListFolders();
                    if (folders == null)
                    {
                        _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Could not Access Drive. [{code}]");
                        return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}&message=Could not Access Drive.");
                    }
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
                    _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                    return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
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
                    _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Could not find {accountFolderId} folder");
                    return Json($"Could not find {accountFolderId} folder");
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
                    _context.Vtex.Logger.Info("ProcessReturnCode", null, $"Could not find {imagesFolderId} folder");
                    return Json($"Could not find {imagesFolderId} folder");
                }

                folders = await _googleDriveService.ListFolders(imagesFolderId);

                if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
                {
                    newFolderId = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
                    bool setPermission = await _googleDriveService.SetPermission(newFolderId);
                    if (!setPermission)
                    {
                        _context.Vtex.Logger.Error("ProcessReturnCode", "SetPermission", $"Could not set permissions on '{DriveImportConstants.FolderNames.NEW}' folder {newFolderId}");
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

                folderIds = new FolderIds
                {
                    AccountFolderId = accountFolderId,
                    DoneFolderId = doneFolderId,
                    ErrorFolderId = errorFolderId,
                    ImagesFolderId = imagesFolderId,
                    ImportFolderId = imagesFolderId,
                    NewFolderId = newFolderId
                };

                bool folderIdsSaved = await _driveImportRepository.SaveFolderIds(folderIds, accountName);
                _context.Vtex.Logger.Error("ProcessReturnCode", null, $"Folder Ids Saved? {folderIdsSaved}");
            }

            GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId, true);
            watch = (googleWatch != null);
            _context.Vtex.Logger.Error("ProcessReturnCode", null, $"Folder [{newFolderId}] Watch Set? {watch}");
            if (watch)
            {
                long expiresIn = googleWatch.Expiration ?? 0;
                if (expiresIn > 0)
                {
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(expiresIn);
                    DateTime expiresAt = dateTimeOffset.UtcDateTime;
                    Console.WriteLine($"expiresAt = {expiresAt}");
                    CreateTask(expiresAt);
                }
            }

            return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}");
        }

        public async Task<IActionResult> GoogleAuthorize()
        {
            string url = await _googleDriveService.GetGoogleAuthorizationUrl();
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest();
            }
            else
            {
                return Redirect(url);
            }
        }

        public async Task SaveCredentials()
        {
            if ("post".Equals(HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
            {
                string bodyAsText = await new System.IO.StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                Credentials credentials = JsonConvert.DeserializeObject<Credentials>(bodyAsText);

                await _googleDriveService.SaveCredentials(credentials);
            }
        }

        public async Task<IActionResult> ListFiles()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
            return Json(await _googleDriveService.ListFiles());
        }

        public async Task<IActionResult> ListImages()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
            string newFolderId;
            string doneFolderId;
            string errorFolderId;
            string importFolderId;
            string accountFolderId;
            string imagesFolderId;
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
                Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name
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
                    return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
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
                    return Json($"Could not find {accountFolderId} folder");
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
                    return Json($"Could not find {imagesFolderId} folder");
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

                _driveImportRepository.SaveFolderIds(folderIds, accountName);
            }

            Dictionary<string, string> images = new Dictionary<string, string>();
            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);

            return Json(imageFiles);
        }

        public async Task<bool> HaveToken()
        {
            bool haveToken = false;
            Token token = await _googleDriveService.GetGoogleToken();
            haveToken = token != null && !string.IsNullOrEmpty(token.RefreshToken);
            Console.WriteLine($"Have Token? {haveToken}");
            Response.Headers.Add("Cache-Control", "no-cache");
            return haveToken;
        }

        public async Task<IActionResult> GetOwners()
        {
            ListFilesResponse listFilesResponse = await _googleDriveService.ListFiles();
            var owners = listFilesResponse.Files.Select(o => o.Owners.Distinct());
            Response.Headers.Add("Cache-Control", "no-cache");
            return Json(owners);
        }

        public async Task<IActionResult> GetOwnerEmail()
        {
            string email = null;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            try
            {
                Token token = await _googleDriveService.GetGoogleToken();
                if (token != null)
                {
                    //if (string.IsNullOrEmpty(token.RefreshToken))
                    //{
                    //    bool revoked = await _googleDriveService.RevokeGoogleAuthorizationToken();
                    //    if (!revoked)
                    //    {
                    //        for (int i = 1; i < 5; i++)
                    //        {
                    //            Console.WriteLine($"RevokeGoogleAuthorizationToken Retry #{i}");
                    //            _context.Vtex.Logger.Info("GetOwnerEmail", "RevokeGoogleAuthorizationToken", $"Retry #{i}");
                    //            await Task.Delay(500 * i);
                    //            revoked = await _googleDriveService.RevokeGoogleAuthorizationToken();
                    //            if (revoked)
                    //            {
                    //                break;
                    //            }
                    //        }
                    //    }

                    //    _context.Vtex.Logger.Info("GetOwnerEmail", null, $"Revoked Token? {revoked}");

                    //    if (revoked)
                    //    {
                    //        await _driveImportRepository.SaveFolderIds(null, accountName);
                    //    }

                    //    return Json(null);
                    //}

                    string newFolderId = string.Empty;
                    FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
                    if (folderIds != null)
                    {
                        newFolderId = folderIds.NewFolderId;
                        Console.WriteLine($"GetOwnerEmail - newFolderId = {newFolderId}");
                        _context.Vtex.Logger.Info("GetOwnerEmail", null, $"New Folder Id: {newFolderId}");
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not load folder structure from storage.");
                        newFolderId = await _googleDriveService.FindNewFolderId(accountName);
                        Console.WriteLine($"GetOwnerEmail - FindNewFolderId = {newFolderId}");
                    }

                    //Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name
                    //string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
                    ListFilesResponse listFilesResponse = await _googleDriveService.ListFiles();
                    if (listFilesResponse != null)
                    {
                        var owners = listFilesResponse.Files.Where(f => f.Id.Equals(newFolderId)).Select(o => o.Owners.Distinct()).FirstOrDefault();
                        if (owners != null)
                        {
                            email = owners.Select(o => o.EmailAddress).FirstOrDefault();
                        }
                        else
                        {
                            _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not find owners. (1)");
                            Console.WriteLine("GetOwnerEmail - Could not find owners. (1)");
                            newFolderId = await _googleDriveService.FindNewFolderId(accountName);
                            owners = listFilesResponse.Files.Where(f => f.Id.Equals(newFolderId)).Select(o => o.Owners.Distinct()).FirstOrDefault();
                            if (owners != null)
                            {
                                email = owners.Select(o => o.EmailAddress).FirstOrDefault();
                            }
                            else
                            {
                                _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not find owners. (2)");
                                Console.WriteLine("GetOwnerEmail - Could not find owners. (2)");
                            }
                        }
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not load Token.");
                    Console.WriteLine("GetOwnerEmail - Could not load Token.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                _context.Vtex.Logger.Error("GetOwnerEmail", null, $"Error getting Drive owner", ex);
            }

            _context.Vtex.Logger.Info("GetOwnerEmail", null, $"Email = {email}");

            Response.Headers.Add("Cache-Control", "no-cache");
            return Json(email);
        }

        public async Task<IActionResult> RevokeToken()
        {
            bool revoked = false;
            revoked = await _googleDriveService.RevokeGoogleAuthorizationToken();
            if (!revoked)
            {
                for (int i = 1; i < 5; i++)
                {
                    Console.WriteLine($"RevokeGoogleAuthorizationToken Retry #{i}");
                    _context.Vtex.Logger.Info("GetOwnerEmail", "RevokeGoogleAuthorizationToken", $"Retry #{i}");
                    await Task.Delay(500 * i);
                    revoked = await _googleDriveService.RevokeGoogleAuthorizationToken();
                    if (revoked)
                    {
                        break;
                    }
                }
            }

            if(revoked)
            {
                string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
                await _driveImportRepository.SaveFolderIds(null, accountName);
            }

            Response.Headers.Add("Cache-Control", "no-cache");
            return Json(revoked);
        }

        public async Task<IActionResult> SetWatch()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
            return Json("disabled");
            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name
            string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
            GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId);
            bool watch = googleWatch != null;
            if (watch)
            {
                long expiresIn = googleWatch.Expiration ?? 0;
                if (expiresIn > 0)
                {
                    DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(expiresIn);
                    DateTime expiresAt = dateTimeOffset.UtcDateTime;
                    Console.WriteLine($"expiresAt = {expiresAt}");
                    CreateTask(expiresAt);
                }
            }

            return Json(googleWatch);
        }

        private void SetWatchAfterDelay(int dueTime)
        {
            Console.WriteLine($"Re-setting Watch in {TimeSpan.FromMilliseconds(dueTime)}......");
            Timer timer = new Timer(ThreadFunc, null, dueTime, Timeout.Infinite);
            Timer keepAwake = new Timer(KeepAwake, null, 0, Timeout.Infinite);
        }

        private void ThreadFunc(object state)
        {
            Console.WriteLine("Re-setting Watch......");
            this.SetWatch();
        }

        private void KeepAwake(object state)
        {
            while (true)
            {
                Console.WriteLine($"Keep Awake {DateTime.Now.TimeOfDay}");
                Thread.Sleep(5 * 1000 * 60);
            }
        }

        private void CreateTask(DateTime expiresAt)
        {
            DateTime state = expiresAt;
            Timer keepAwake = new Timer(ReSetWatch, state, 0, Timeout.Infinite);
        }

        private void ReSetWatch(object state)
        {
            DateTime expiresAt = (DateTime)state;
            int window = 5;
            while (true)
            {
                if (DateTime.Now >= expiresAt.AddMinutes(window))
                {
                    SetWatch();
                    break;
                }

                Console.WriteLine($"Keep Awake {DateTime.Now.TimeOfDay} ({window} minutes)");
                Thread.Sleep(window * 1000 * 60);
            }
        }

        public async Task ProcessChange()
        {
            if ("post".Equals(HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
            {
                var headers = HttpContext.Request.Headers;
                if (!string.IsNullOrEmpty(headers["x-goog-resource-state"])
                    && !string.IsNullOrEmpty(headers["x-goog-changed"]))
                {
                    if (headers["x-goog-resource-state"] == "update" && headers["x-goog-changed"] == "children")
                    {
                        Console.WriteLine("Triggered");
                        _context.Vtex.Logger.Debug("ProcessChange", null, "Received Watch Notification");
                        //Console.Write(PrintHeaders());
                        // await _driveImportRepository.ClearImportLock();

                        //string watchExpiresAtHeader = headers["x-goog-channel-expiration"];
                        //DateTime watchExpiresAt = DateTime.Now;
                        //if (!string.IsNullOrEmpty(watchExpiresAtHeader))
                        //{
                        //    DateTime.TryParse(watchExpiresAtHeader, out watchExpiresAt);
                        //    Console.WriteLine($"ExpiresAt {watchExpiresAt} ({watchExpiresAtHeader})  {watchExpiresAt - DateTime.Now}");
                        //    //int timeInMilliseconds = (int)(watchExpiresAt - DateTime.Now).TotalMilliseconds;
                        //    // SetWatchAfterDelay(timeInMilliseconds);
                        //    //_driveImportRepository.SetWatchExpiration(watchExpiresAt);
                        //}

                        // check lock
                        //DateTime importStarted = await _driveImportRepository.CheckImportLock();

                        //Console.WriteLine($"Check lock: {importStarted}");

                        //TimeSpan elapsedTime = DateTime.Now - importStarted;
                        //if (elapsedTime.TotalHours < 2)
                        //{
                        //    Console.WriteLine("Blocked by lock");
                        //    _context.Vtex.Logger.Info("ProcessChange", null, $"Blocked by lock.  Import started: {importStarted}");
                        //    return;
                        //}

                        //await _driveImportRepository.SetImportLock(DateTime.Now);
                        //Console.WriteLine($"Set new lock: {DateTime.Now}");
                        //_context.Vtex.Logger.Info("ProcessChange", null, $"Set new lock: {DateTime.Now}");

                        await DriveImport();
                        //ClearLockAfterDelay(5000);
                    }
                }
            }
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
            catch(Exception ex)
            {
                _context.Vtex.Logger.Error("DriveImport", null, "Failed to clear lock", ex);
            }
        }

        public string PrintHeaders()
        {
            string headers = "--->>> Headers <<<---\n";
            foreach (var header in HttpContext.Request.Headers)
            {
                headers += $"{header.Key}: {header.Value}\n";
            }
            return headers;
        }

        public async Task ClearLock()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
            Console.WriteLine($"CheckImportLock: {await _driveImportRepository.CheckImportLock()}");
            await _driveImportRepository.ClearImportLock();
            Console.WriteLine($"CheckImportLock: {await _driveImportRepository.CheckImportLock()}");
        }

        public async Task<IActionResult> CreateSheet()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
            string sheetUrl = string.Empty;
            string sheetName = "VtexImageImport";
            string sheetLabel = "ImagesForImport";
            string[] headerRowLabels = new string[]
                {
                    "Type","Value","Name","Main","Image","Context","Status","Message","Date"
                };

            GoogleSheetCreate googleSheetCreate = new GoogleSheetCreate
            {
                Properties = new GoogleSheetProperties
                {
                    Title = sheetName
                },
                Sheets = new Sheet[]
                {
                    new Sheet
                    {
                        Properties = new SheetProperties
                        {
                            SheetId = 0,
                            Title = sheetLabel,
                            Index = 0,
                            GridProperties = new GridProperties
                            {
                                ColumnCount = headerRowLabels.Count(),
                                RowCount = 3000
                            },
                            SheetType = "GRID"
                        },
                        ConditionalFormats = new ConditionalFormat[]
                        {
                            new ConditionalFormat
                            {
                                BooleanRule = new BooleanRule
                                {
                                    Condition = new Condition
                                    {
                                        Type = "TEXT_CONTAINS",
                                        Values = new Value[]
                                        {
                                            new Value
                                            {
                                                UserEnteredValue = "Error"
                                            }
                                        }
                                    },
                                    Format = new Format
                                    {
                                        BackgroundColor = new BackgroundColorClass
                                        {
                                            Blue = 0.6,
                                            Green = 0.6,
                                            Red = 0.91764706
                                        },
                                        BackgroundColorStyle = new BackgroundColorStyle
                                        {
                                            RgbColor = new BackgroundColorClass
                                            {
                                                Blue = 0.6,
                                                Green = 0.6,
                                                Red = 0.91764706
                                            }
                                        },
                                        TextFormat = new FormatTextFormat
                                        {
                                            ForegroundColor = new BackgroundColorClass(),
                                            ForegroundColorStyle = new BackgroundColorStyle
                                            {
                                                RgbColor = new BackgroundColorClass()
                                            }
                                        }
                                    }
                                },
                                Ranges = new CreateRange[]
                                {
                                    new CreateRange
                                    {
                                        EndColumnIndex = 7,
                                        EndRowIndex = 3000,
                                        StartColumnIndex = 6,
                                        StartRowIndex = 1
                                    }
                                }
                            },
                            new ConditionalFormat
                            {
                                BooleanRule = new BooleanRule
                                {
                                    Condition = new Condition
                                    {
                                        Type = "TEXT_CONTAINS",
                                        Values = new Value[]
                                        {
                                            new Value
                                            {
                                                UserEnteredValue = "Done"
                                            }
                                        }
                                    },
                                    Format = new Format
                                    {
                                        BackgroundColor = new BackgroundColorClass
                                        {
                                            Blue = 0.8039216,
                                            Green = 0.88235295,
                                            Red = 0.7176471
                                        },
                                        BackgroundColorStyle = new BackgroundColorStyle
                                        {
                                            RgbColor = new BackgroundColorClass
                                            {
                                                Blue = 0.8039216,
                                                Green = 0.88235295,
                                                Red = 0.7176471
                                            }
                                        },
                                        TextFormat = new FormatTextFormat
                                        {
                                            ForegroundColor = new BackgroundColorClass(),
                                            ForegroundColorStyle = new BackgroundColorStyle
                                            {
                                                RgbColor = new BackgroundColorClass()
                                            }
                                        }
                                    }
                                },
                                Ranges = new CreateRange[]
                                {
                                    new CreateRange
                                    {
                                        EndColumnIndex = 7,
                                        EndRowIndex = 3000,
                                        StartColumnIndex = 6,
                                        StartRowIndex = 1
                                    }
                                }
                            }
                        }
                    }
                }
            };

            string sheetId = await _googleDriveService.CreateSpreadsheet(googleSheetCreate);

            if(!string.IsNullOrEmpty(sheetId))
            {
                string lastHeaderColumnLetter = ((char)headerRowLabels.Count() + 65).ToString();

                ValueRange valueRange = new ValueRange
                {
                    MajorDimension = "ROWS",
                    Range = $"{sheetLabel}!A1:{lastHeaderColumnLetter}1",
                    Values = new string[][]
                    {
                        headerRowLabels
                    }
                };

                UpdateValuesResponse updateValuesResponse = await _googleDriveService.WriteSpreadsheetValues(sheetId, valueRange);

                BatchUpdate batchUpdate = new BatchUpdate
                {
                    Requests = new Request[]
                    {
                        new Request
                        {
                            RepeatCell = new RepeatCell
                            {
                                Cell = new Cell
                                {
                                    UserEnteredFormat = new UserEnteredFormat
                                    {
                                        HorizontalAlignment = "CENTER",
                                        BackgroundColor = new GroundColor
                                        {
                                            Blue = 0.0,
                                            Green = 0.0,
                                            Red = 0.0
                                        },
                                        TextFormat = new BatchUpdateTextFormat
                                        {
                                            Bold = true,
                                            FontSize = 12,
                                            ForegroundColor = new GroundColor
                                            {
                                                Blue = 1.0,
                                                Green = 1.0,
                                                Red = 1.0
                                            }
                                        }
                                    }
                                },
                                Fields = "userEnteredFormat(backgroundColor,textFormat,horizontalAlignment)",
                                Range = new BatchUpdateRange
                                {
                                    StartRowIndex = 0,
                                    EndRowIndex = 1,
                                    SheetId = 0
                                }
                            }
                        },
                        new Request
                        {
                            UpdateSheetProperties = new UpdateSheetProperties
                            {
                                Fields = "gridProperties.frozenRowCount",
                                Properties = new Properties
                                {
                                    SheetId = 0,
                                    GridProperties = new BatchUpdateGridProperties
                                    {
                                        FrozenRowCount = 1
                                    }
                                }
                            }
                        }
                    }
                };

                var updateSheet = await _googleDriveService.UpdateSpreadsheet(sheetId, batchUpdate);

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

                    bool moved = await _googleDriveService.MoveFile(sheetId, imagesFolderId);
                    Console.WriteLine($"Moved? {moved}");
                }
            }

            string result = string.IsNullOrEmpty(sheetId) ? "Error" : "Created";
            return Json(result);
        }

        public async Task<string> GetSheetLink()
        {
            string sheetUrl = string.Empty;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                string imagesFolderId = folderIds.ImagesFolderId;
                ListFilesResponse spreadsheets = await _googleDriveService.ListSheetsInFolder(imagesFolderId);
                List<string> links = new List<string>();
                if (spreadsheets != null)
                {
                    foreach (GoogleFile file in spreadsheets.Files)
                    {
                        links.Add(file.WebViewLink.ToString());
                    }

                    sheetUrl = string.Join("<br>", links);
                }
            }

            return sheetUrl;
        }
    }
}
