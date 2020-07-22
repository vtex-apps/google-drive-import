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

        public RoutesController(IIOServiceContext context, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory, IGoogleDriveService googleDriveService, IVtexAPIService vtexAPIService)
        {
            this._context = context ?? throw new ArgumentNullException(nameof(context));
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            this._clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            this._googleDriveService = googleDriveService ?? throw new ArgumentNullException(nameof(googleDriveService));
            this._vtexAPIService = vtexAPIService ?? throw new ArgumentNullException(nameof(vtexAPIService));

            // DoWork();
        }

        public async Task DoWork()
        {
            Console.WriteLine("DoWork Init");
            TimeSpan timeSpan = new TimeSpan(0, 5, 0);
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine($"------------------>>>>>>>>>>>>>>>>> {i} <<<<<<<<<<<<<<<<<<<<<----------------------------");
                await Task.Delay(timeSpan);
            }
        }

        public async Task<IActionResult> DriveImport()
        {
            bool updated = false;
            int doneCount = 0;
            int errorCount = 0;
            //TimeSpan timeSpan = new TimeSpan(0, 30, 0);

            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

            bool relistFolders = false;
            if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE);
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
            {
                relistFolders = await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR);
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
                _googleDriveService.SetPermission(newFolderId);
            }

            //Console.WriteLine($"{doneFolderId} {errorFolderId}");

            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            if (imageFiles != null)
            {
                foreach (Models.GoogleFile file in imageFiles.Files)
                {
                    Console.WriteLine($"'{file.Name}' [{file.Id}]");
                    //byte[] imageStream = await _googleDriveService.GetFile(file.Id);
                    if (!string.IsNullOrEmpty(file.WebContentLink.ToString()))
                    {
                        //updated = await _vtexAPIService.ProcessImageFile(file.Name, imageStream);
                        UpdateResponse updateResponse = await _vtexAPIService.ProcessImageFile(file.Name, file.WebContentLink.ToString());
                        updated = updateResponse.Success;
                        if (updated)
                        {
                            doneCount++;
                            await _googleDriveService.MoveFile(file.Id, doneFolderId);
                        }
                        else
                        {
                            errorCount++;
                            await _googleDriveService.MoveFile(file.Id, errorFolderId);
                            string errorText = updateResponse.Message.Replace(" ", "_").Replace("\"", "");
                            string newFileName = $"{file.Name}-{errorText}";
                            await _googleDriveService.RenameFile(file.Id, newFileName);
                        }
                    }
                }
            }

            Response.Headers.Add("Cache-Control", "no-cache");
            return Json($"Imported {doneCount} image(s).  {errorCount} image(s) not imported.");
        }

        public async Task<IActionResult> ProcessReturnUrl()
        {
            Console.WriteLine("ProcessReturnUrl");
            foreach (string key in _httpContextAccessor.HttpContext.Request.Query.Keys)
            {
                Console.WriteLine($"-]|[- {key} = {_httpContextAccessor.HttpContext.Request.Query[key]}");
            }

            string code = _httpContextAccessor.HttpContext.Request.Query["code"];
            string siteUrl = _httpContextAccessor.HttpContext.Request.Query["state"];
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
            Console.WriteLine("ProcessReturnCode");
            foreach (string key in _httpContextAccessor.HttpContext.Request.Query.Keys)
            {
                Console.WriteLine($"-]|[- {key} = {_httpContextAccessor.HttpContext.Request.Query[key]}");
            }

            string code = _httpContextAccessor.HttpContext.Request.Query["code"];
            if (!string.IsNullOrEmpty(code))
            {
                success = await _googleDriveService.ProcessReturn(code);
            }

            string siteUrl = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.FORWARDED_HOST];
            return Redirect($"https://{siteUrl}/{DriveImportConstants.ADMIN_PAGE}?success={success}");
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
                Console.WriteLine($"[Credentials] : '{bodyAsText}'");
                Credentials credentials = JsonConvert.DeserializeObject<Credentials>(bodyAsText);

                await _googleDriveService.SaveCredentials(credentials);
            }
        }

        public async Task<string> ListFiles()
        {
            Console.WriteLine("ListFiles.........");
            Response.Headers.Add("Cache-Control", "no-cache");
            return await _googleDriveService.ListFiles();
        }

        public async Task<IActionResult> ListImages()
        {
            Console.WriteLine("ListImages.........");
            Response.Headers.Add("Cache-Control", "no-cache");
            Dictionary<string, string> folders = await _googleDriveService.ListFolders();   // Id, Name

            bool relistFolders = false;
            if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            {
                await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW);
                relistFolders = true;
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.DONE))
            {
                await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.DONE);
                relistFolders = true;
            }

            if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
            {
                await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.ERROR);
                relistFolders = true;
            }

            if(relistFolders)
            {
                folders = await _googleDriveService.ListFolders();
            }

            string newFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.NEW).Key;

            //ListFilesResponse imageFiles = await _googleDriveService.ListImages();
            Dictionary<string, string> images = new Dictionary<string, string>();
            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInFolder(newFolderId);
            //if (imageFiles != null)
            //{
            //    string doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
            //    string errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;
            //    Console.WriteLine($"{doneFolderId} {errorFolderId}");
            //    foreach (Models.File file in imageFiles.Files)
            //    {
            //        Console.WriteLine($"{file.Name} {file.WebViewLink}");
            //        await _googleDriveService.MoveFile(file.Id, doneFolderId);
            //    }
            //}

            return Json(imageFiles);
        }

        public async Task<bool> HaveToken()
        {
            Token token = await _googleDriveService.GetGoogleToken();
            return token != null && !string.IsNullOrEmpty(token.RefreshToken);
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
