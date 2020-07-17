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

        public RoutesController(IIOServiceContext context, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory, IGoogleDriveService googleDriveService)
        {
            this._context = context ?? throw new ArgumentNullException(nameof(context));
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            this._clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            this._googleDriveService = googleDriveService ?? throw new ArgumentNullException(nameof(googleDriveService));
        }

        public async Task<IActionResult> DriveImport()
        {
            string url = "";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };

            request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
            string authToken = _context.Vtex.AuthToken;
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"{response.StatusCode}");
            return Json("");
        }

        public async Task<IActionResult> ProcessReturnUrl()
        {
            bool success = false;
            foreach(string key in _httpContextAccessor.HttpContext.Request.Query.Keys)
            {
                Console.WriteLine($"-]|[- {key} = {_httpContextAccessor.HttpContext.Request.Query[key]}");
            }

            string code = _httpContextAccessor.HttpContext.Request.Query["code"];
            if (!string.IsNullOrEmpty(code))
            {
                success = await _googleDriveService.ProcessReturn(code);
            }

            return Json(success);
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
            //if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
            //{
            //    await _googleDriveService.CreateFolder(DriveImportConstants.FolderNames.NEW);
            //    relistFolders = true;
            //}

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

            //ListFilesResponse imageFiles = await _googleDriveService.ListImages();
            Dictionary<string, string> images = new Dictionary<string, string>();
            ListFilesResponse imageFiles = await _googleDriveService.ListImagesInRootFolder();
            if (imageFiles != null)
            {
                string doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
                string errorFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.ERROR).Key;
                Console.WriteLine($"{doneFolderId} {errorFolderId}");
                foreach (Models.File file in imageFiles.Files)
                {
                    Console.WriteLine($"{file.Name} {file.WebViewLink}");
                    await _googleDriveService.MoveFile(file.Id, doneFolderId);
                }
            }

            return Json(imageFiles);
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
