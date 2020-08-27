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

                _driveImportRepository.SaveFolderIds(folderIds, accountName);
            }

            Dictionary<string, string> images = new Dictionary<string, string>();

            GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId);

            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            if (imageFiles != null)
            {
                bool thereAreFiles = imageFiles.Files.Count > 0;
                while (thereAreFiles)
                {
                    bool moveFailed = false;
                    _context.Vtex.Logger.Info("DriveImport", null, $"Processing {imageFiles.Files.Count} files.");
                    foreach (GoogleFile file in imageFiles.Files)
                    {
                        _context.Vtex.Logger.Info("DriveImport", null, $"Beginning Processing of '{file.Name}' at {file.WebContentLink}");
                        if (!string.IsNullOrEmpty(file.WebContentLink.ToString()))
                        {
                            UpdateResponse updateResponse = await _vtexAPIService.ProcessImageFile(file.Name, file.WebContentLink.ToString());
                            _context.Vtex.Logger.Info("DriveImport", "UpdateResponse", $"'{file.Name}' Response: {JsonConvert.SerializeObject(updateResponse)}");
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
                                    moveFailed = true;
                                }
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
                                    moveFailed = true;
                                }
                            }
                        }
                    }

                    if (moveFailed)
                    {
                        thereAreFiles = false;
                    }
                    else
                    {
                        await Task.Delay(5000);
                        imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
                        if (imageFiles == null)
                        {
                            thereAreFiles = false;
                        }
                        else
                        {
                            thereAreFiles = imageFiles.Files.Count > 0;
                        }
                    }

                    Console.WriteLine($"Loop again? {thereAreFiles}");
                }
            }

            if (doneCount + errorCount > 0)
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Imported {doneCount} image(s).  {errorCount} image(s) not imported.  Done:[{string.Join(" ", doneFileNames)}] Error:[{string.Join(" ", errorFileNames)}]");
            }

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
                return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}&message=Missing return code.");
            }

            success = await _googleDriveService.ProcessReturn(code);

            if (!success)
            {
                return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}&watch={watch}&message=Could not process code.");
            }

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

                if (folders == null)
                {
                    await Task.Delay(500);
                    folders = await _googleDriveService.ListFolders();
                    if (folders == null)
                    {
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

            GoogleWatch googleWatch = await _googleDriveService.SetWatch(newFolderId, true);
            watch = (googleWatch != null);
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
            Token token = await _googleDriveService.GetGoogleToken();
            return token != null && !string.IsNullOrEmpty(token.RefreshToken);
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
            try
            {
                Token token = await _googleDriveService.GetGoogleToken();
                if (token != null)
                {
                    if (string.IsNullOrEmpty(token.RefreshToken))
                    {
                        bool revoked = await _googleDriveService.RevokeGoogleAuthorizationToken();
                        _context.Vtex.Logger.Info("GetOwnerEmail", null, $"Revoked Token? {revoked}");
                        if(revoked)
                        {
                            await this._driveImportRepository.SaveToken(new Token());
                        }

                        return Json(null);
                    }

                    string newFolderId = string.Empty;
                    string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
                    FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
                    if (folderIds != null)
                    {
                        newFolderId = folderIds.NewFolderId;
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
                            _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not owner.");
                        }
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("GetOwnerEmail", null, "Could not load Token.");
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
            bool success = false;
            success = await _googleDriveService.RevokeGoogleAuthorizationToken();
            Response.Headers.Add("Cache-Control", "no-cache");
            return Json(success);
        }

        public async Task<IActionResult> SetWatch()
        {
            Response.Headers.Add("Cache-Control", "no-cache");
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
                        //Console.Write(PrintHeaders());
                        // await _driveImportRepository.ClearImportLock();

                        string watchExpiresAtHeader = headers["x-goog-channel-expiration"];
                        DateTime watchExpiresAt = DateTime.Now;
                        if (!string.IsNullOrEmpty(watchExpiresAtHeader))
                        {
                            DateTime.TryParse(watchExpiresAtHeader, out watchExpiresAt);
                            Console.WriteLine($"ExpiresAt {watchExpiresAt} ({watchExpiresAtHeader})");
                            //int timeInMilliseconds = (int)(watchExpiresAt - DateTime.Now).TotalMilliseconds;
                            // SetWatchAfterDelay(timeInMilliseconds);
                            //_driveImportRepository.SetWatchExpiration(watchExpiresAt);
                        }

                        // check lock
                        DateTime importStarted = await _driveImportRepository.CheckImportLock();

                        Console.WriteLine($"Check lock: {importStarted}");

                        TimeSpan elapsedTime = DateTime.Now - importStarted;
                        if (elapsedTime.TotalHours < 2)
                        {
                            Console.WriteLine("Blocked by lock");
                            _context.Vtex.Logger.Info("ProcessChange", null, $"Blocked by lock.  Import started: {importStarted}");
                            return;
                        }

                        await _driveImportRepository.SetImportLock(DateTime.Now);
                        Console.WriteLine($"Set new lock: {DateTime.Now}");
                        _context.Vtex.Logger.Info("ProcessChange", null, $"Set new lock: {DateTime.Now}");

                        await DriveImport();
                        await Task.Delay(5000);
                        await _driveImportRepository.ClearImportLock();
                        Console.WriteLine("Cleared lock");
                        _context.Vtex.Logger.Info("ProcessChange", null, $"Cleared lock: {DateTime.Now}");
                    }
                }
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
    }
}
