﻿namespace DriveImport.Controllers
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

            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

            bool relistFolders = false;
            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
            if(string.IsNullOrEmpty(importFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
            }

            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            if (!folders.ContainsValue(accountName))
            {
                relistFolders = await _googleDriveService.CreateFolder(accountName, importFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
            if (string.IsNullOrEmpty(accountFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {accountFolderId} folder");
                return Json($"Could not find {accountFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
            if (string.IsNullOrEmpty(imagesFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {imagesFolderId} folder");
                return Json($"Could not find {imagesFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
            }

            if (relistFolders)
            {
                folders = await _googleDriveService.ListFolders();
            }

            //ListFilesResponse imageFiles = await _googleDriveService.ListImages();
            Dictionary<string, string> images = new Dictionary<string, string>();

            string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
            string doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
            string errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;

            if (relistFolders)
            {
                bool setPermission = await _googleDriveService.SetPermission(newFolderId);
                if (!setPermission)
                {
                    _context.Vtex.Logger.Error("DriveImport", "SetPermission", $"Could not set permissions on '{DriveImportConstants.FolderNames.NEW}' folder {newFolderId}");
                }
            }

            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            if (imageFiles != null)
            {
                bool thereAreFiles = imageFiles.Files.Count > 0;
                while (thereAreFiles)
                {
                    bool moveFailed = false;
                    foreach (GoogleFile file in imageFiles.Files)
                    {
                        if (!string.IsNullOrEmpty(file.WebContentLink.ToString()))
                        {
                            UpdateResponse updateResponse = await _vtexAPIService.ProcessImageFile(file.Name, file.WebContentLink.ToString());
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

            string code = _httpContextAccessor.HttpContext.Request.Query["code"];

            _context.Vtex.Logger.Info("ProcessReturnCode", null, $"code=[{code}]");

            if (!string.IsNullOrEmpty(code))
            {
                success = await _googleDriveService.ProcessReturn(code);
            }

            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

            bool relistFolders = false;
            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
            if (string.IsNullOrEmpty(importFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
            }

            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            if (!folders.ContainsValue(accountName))
            {
                relistFolders = await _googleDriveService.CreateFolder(accountName, importFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
            if (string.IsNullOrEmpty(accountFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {accountFolderId} folder");
                return Json($"Could not find {accountFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
            if (string.IsNullOrEmpty(imagesFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {imagesFolderId} folder");
                return Json($"Could not find {imagesFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
            }

            if (relistFolders)
            {
                folders = await _googleDriveService.ListFolders();
            }

            //ListFilesResponse imageFiles = await _googleDriveService.ListImages();
            Dictionary<string, string> images = new Dictionary<string, string>();

            string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
            string doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
            string errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;

            if (relistFolders)
            {
                bool setPermission = await _googleDriveService.SetPermission(newFolderId);
                if (!setPermission)
                {
                    _context.Vtex.Logger.Error("DriveImport", "SetPermission", $"Could not set permissions on '{DriveImportConstants.FolderNames.NEW}' folder {newFolderId}");
                }
            }

            bool watch = await _googleDriveService.SetWatch(newFolderId);

            string siteUrl = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.FORWARDED_HOST];

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
            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

            bool relistFolders = false;
            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
            if (string.IsNullOrEmpty(importFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                return Json($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
            }

            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            if (!folders.ContainsValue(accountName))
            {
                relistFolders = await _googleDriveService.CreateFolder(accountName, importFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
            if (string.IsNullOrEmpty(accountFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {accountFolderId} folder");
                return Json($"Could not find {accountFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                if (relistFolders)
                {
                    folders = await _googleDriveService.ListFolders();
                }
            }

            string imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
            if (string.IsNullOrEmpty(imagesFolderId))
            {
                _context.Vtex.Logger.Info("DriveImport", null, $"Could not find {imagesFolderId} folder");
                return Json($"Could not find {imagesFolderId} folder");
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
            }

            if (relistFolders)
            {
                folders = await _googleDriveService.ListFolders();
            }

            string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;

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
                if (token != null && !string.IsNullOrEmpty(token.RefreshToken))
                {
                    Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name
                    string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;
                    ListFilesResponse listFilesResponse = await _googleDriveService.ListFiles();
                    if (listFilesResponse != null)
                    {
                        var owners = listFilesResponse.Files.Where(f => f.Id.Equals(newFolderId)).Select(o => o.Owners.Distinct()).FirstOrDefault();
                        if (owners != null)
                        {
                            email = owners.Select(o => o.EmailAddress).FirstOrDefault();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }

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
            return Json(await _googleDriveService.SetWatch(newFolderId));
        }

        public void SetWatchAfterDelay(int dueTime)
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

        public async Task ProcessChange()
        {
            if ("post".Equals(HttpContext.Request.Method, StringComparison.OrdinalIgnoreCase))
            {
                // string bodyAsText = await new System.IO.StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                // GoogleWatch watch = JsonConvert.DeserializeObject<GoogleWatch>(bodyAsText);
                // Console.WriteLine($"Watch: {bodyAsText}");
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
                            int timeInMilliseconds = (int)(watchExpiresAt - DateTime.Now).TotalMilliseconds;
                            // SetWatchAfterDelay(timeInMilliseconds);
                        }

                        // check lock
                        DateTime importStarted = await _driveImportRepository.CheckImportLock();

                        Console.WriteLine($"Check lock: {importStarted}");

                        TimeSpan elapsedTime = DateTime.Now - importStarted;
                        if (elapsedTime.TotalHours < 2)
                        {
                            Console.WriteLine("Blocked by lock");
                            return;
                        }

                        await _driveImportRepository.SetImportLock(DateTime.Now);
                        Console.WriteLine($"Set new lock: {DateTime.Now}");

                        await DriveImport();
                        await Task.Delay(5000);
                        await _driveImportRepository.ClearImportLock();
                        Console.WriteLine("Cleared lock");
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
