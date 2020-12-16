using DriveImport.Data;
using DriveImport.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Vtex.Api.Context;

namespace DriveImport.Services
{
    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly IIOServiceContext _context;
        private readonly IVtexEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IDriveImportRepository _driveImportRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _applicationName;

        public GoogleDriveService(IIOServiceContext context, IVtexEnvironmentVariableProvider environmentVariableProvider, IDriveImportRepository driveImportRepository, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory)
        {
            this._context = context ??
                            throw new ArgumentNullException(nameof(context));

            this._environmentVariableProvider = environmentVariableProvider ??
                                                throw new ArgumentNullException(nameof(environmentVariableProvider));

            this._driveImportRepository = driveImportRepository ??
                                                throw new ArgumentNullException(nameof(driveImportRepository));

            this._httpContextAccessor = httpContextAccessor ??
                                        throw new ArgumentNullException(nameof(httpContextAccessor));

            this._clientFactory = clientFactory ??
                               throw new ArgumentNullException(nameof(clientFactory));

            this._applicationName =
                $"{this._environmentVariableProvider.ApplicationVendor}.{this._environmentVariableProvider.ApplicationName}";
        }

        public async Task<string> GetGoogleAuthorizationUrl()
        {
            string authUrl = string.Empty;
            Credentials credentials = await _driveImportRepository.GetCredentials();
            string siteUrl = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.FORWARDED_HOST];
            if (credentials != null && !string.IsNullOrEmpty(credentials.Web.ClientId))
            {
                string redirectUri = $"{DriveImportConstants.REDIRECT_SITE_BASE}/{DriveImportConstants.APP_NAME}/{DriveImportConstants.REDIRECT_PATH}/";
                string clientId = credentials.Web.ClientId;
                authUrl = $"{credentials.Web.AuthUri}?scope={DriveImportConstants.GOOGLE_SCOPE}&response_type={DriveImportConstants.GOOGLE_REPONSE_TYPE}&access_type={DriveImportConstants.GOOGLE_ACCESS_TYPE}&redirect_uri={redirectUri}&client_id={clientId}&state={siteUrl}";
            }

            return authUrl;
        }

        public async Task<Token> GetGoogleAuthorizationToken(string code)
        {
            Token tokenObj = new Token();
            Credentials credentials = await _driveImportRepository.GetCredentials();
            StringBuilder postData = new StringBuilder();
            postData.Append("code=" + HttpUtility.UrlEncode(code) + "&");
            postData.Append("client_id=" + HttpUtility.UrlEncode(credentials.Web.ClientId) + "&");
            postData.Append("client_secret=" + HttpUtility.UrlEncode(credentials.Web.ClientSecret) + "&");
            postData.Append("grant_type=" + HttpUtility.UrlEncode(DriveImportConstants.GRANT_TYPE_AUTH) + "&");
            postData.Append("redirect_uri=" + HttpUtility.UrlEncode($"{DriveImportConstants.REDIRECT_SITE_BASE}/{DriveImportConstants.APP_NAME}/{DriveImportConstants.REDIRECT_PATH}/") + "&");
            postData.Append("to=");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = credentials.Web.TokenUri,
                Content = new StringContent(postData.ToString(), Encoding.UTF8, DriveImportConstants.APPLICATION_FORM)
            };

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();

            try
            {
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"Token = {responseContent}");
                if (response.IsSuccessStatusCode)
                {
                    tokenObj = JsonConvert.DeserializeObject<Token>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Info("GetGoogleAuthorizationToken", null, $"{response.StatusCode} {responseContent}");
                }
            }
            catch(Exception ex)
            {
                _context.Vtex.Logger.Error("GetGoogleAuthorizationToken", null,  $"Error Posting request. {postData}", ex);
                tokenObj = null;
            }

            return tokenObj;
        }

        public async Task<Token> RefreshGoogleAuthorizationToken(string refreshToken)
        {
            Token tokenObj = new Token();
            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("Refresh Token Empty");
                _context.Vtex.Logger.Info("RefreshGoogleAuthorizationToken", null, "Refresh Token Empty");
            }
            else
            {
                Credentials credentials = await _driveImportRepository.GetCredentials();
                StringBuilder postData = new StringBuilder();
                postData.Append("refresh_token=" + HttpUtility.UrlEncode(refreshToken) + "&");
                postData.Append("client_id=" + HttpUtility.UrlEncode(credentials.Web.ClientId) + "&");
                postData.Append("client_secret=" + HttpUtility.UrlEncode(credentials.Web.ClientSecret) + "&");
                postData.Append("grant_type=" + HttpUtility.UrlEncode(DriveImportConstants.GRANT_TYPE_REFRESH));

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = credentials.Web.TokenUri,
                    Content = new StringContent(postData.ToString(), Encoding.UTF8, DriveImportConstants.APPLICATION_FORM)
                };

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"REFRESHED Token = {responseContent}");
                    if (response.IsSuccessStatusCode)
                    {
                        tokenObj = JsonConvert.DeserializeObject<Token>(responseContent);
                        Console.WriteLine($"Refresh Token Response {responseContent}");
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("RefreshGoogleAuthorizationToken", null, $"{response.StatusCode} {responseContent}");
                        Console.WriteLine($"Refresh Token Response [{response.StatusCode}] {responseContent}");
                    }
                }
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("RefreshGoogleAuthorizationToken", null, $"Error refreshing token", ex);
                    Console.WriteLine($"Error refreshing token [{ex.Message}]");
                }
            }

            return tokenObj;
        }

        public async Task<bool> RevokeGoogleAuthorizationToken()
        {
            bool success = false;

            Token token = await _driveImportRepository.LoadToken();

            if (token != null && string.IsNullOrEmpty(token.AccessToken))
            {
                Console.WriteLine("Token Empty");
                _context.Vtex.Logger.Info("RevokeGoogleAuthorizationToken", null, "Token Empty");
                await _driveImportRepository.SaveToken(new Token());
                success = true;
            }
            else
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_AUTHORIZATION_REVOKE}?token={token.AccessToken}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_FORM)
                };

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"RevokeGoogleAuthorizationToken = {responseContent}");
                    if (response.IsSuccessStatusCode)
                    {
                        await _driveImportRepository.SaveToken(new Token());
                        success = true;
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("RevokeGoogleAuthorizationToken", null, $"{response.StatusCode} {responseContent}");
                    }
                }
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("RevokeGoogleAuthorizationToken", null, $"Have Access Token? {!string.IsNullOrEmpty(token.AccessToken)} Have Refresh Token?{!string.IsNullOrEmpty(token.RefreshToken)}", ex);
                }
            }

            return success;
        }

        public async Task<bool> ProcessReturn(string code)
        {
            _context.Vtex.Logger.Info("ProcessReturn", "GoogleDriveService", $"Processing Code [{code}]");
            Token token = await this.GetGoogleAuthorizationToken(code);
            if(token == null)
            {
                for (int i = 1; i < 5; i++)
                {
                    Console.WriteLine($"ProcessReturn Retry #{i}");
                    _context.Vtex.Logger.Info("ProcessReturn", "GoogleDriveService", $"Retry #{i}");
                    await Task.Delay(500 * i);
                    token = await this.GetGoogleAuthorizationToken(code);
                    if(token != null)
                    {
                        break;
                    }
                }
            }

            bool saved = false;
            if (token != null)
            {
                token.ExpiresAt = DateTime.Now.AddSeconds(token.ExpiresIn);
                saved = await _driveImportRepository.SaveToken(token);
                _context.Vtex.Logger.Info("ProcessReturn", "GoogleDriveService", $"Saved? {saved} {JsonConvert.SerializeObject(token)}");
            }

            if (!saved)
            {
                Console.WriteLine($"Did not save token. {JsonConvert.SerializeObject(token)}");
                _context.Vtex.Logger.Info("ProcessReturn", "GoogleDriveService", $"Did not save token. {JsonConvert.SerializeObject(token)}");
            }

            return saved;
        }

        public async Task SaveCredentials(Credentials credentials)
        {
            await _driveImportRepository.SaveCredentials(credentials);
        }

        public async Task<Token> GetGoogleToken()
        {
            Token token = await _driveImportRepository.LoadToken();
            if (!string.IsNullOrEmpty(token.RefreshToken))
            {
                string refreshToken = token.RefreshToken;
                if (token != null) // && !string.IsNullOrEmpty(token.AccessToken))
                {
                    if (token.ExpiresAt <= DateTime.Now)
                    {
                        Console.WriteLine($"ExpiresAt = {token.ExpiresAt} Refreshing token.");
                        token = await this.RefreshGoogleAuthorizationToken(token.RefreshToken);
                        token.ExpiresAt = DateTime.Now.AddSeconds(token.ExpiresIn);
                        if (string.IsNullOrEmpty(token.RefreshToken))
                        {
                            token.RefreshToken = refreshToken;
                        }

                        bool saved = await _driveImportRepository.SaveToken(token);
                    }
                }
                else
                {
                    Console.WriteLine($"Did not load token. Have Access token?{!string.IsNullOrEmpty(token.AccessToken)} Have Refresh token?{!string.IsNullOrEmpty(token.RefreshToken)}");
                    _context.Vtex.Logger.Info("GetGoogleToken", null, $"Could not load token. Have Access token?{!string.IsNullOrEmpty(token.AccessToken)} Have Refresh token?{!string.IsNullOrEmpty(token.RefreshToken)}");
                    token = null;
                }
            }
            else
            {
                _context.Vtex.Logger.Info("GetGoogleToken", null, $"Could not load token.  Refresh token was null. Have Access token?{!string.IsNullOrEmpty(token.AccessToken)}");
            }

            return token;
        }

        public async Task<ListFilesResponse> ListFiles()
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields=*&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"), // RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields=*&q=mimeType contains 'image'"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("ListFiles", null, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("ListFiles", null, $"Error", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("ListFiles", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<Dictionary<string, string>> ListFolders(string parentId = null)
        {
            Dictionary<string, string> folders = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = "mimeType = 'application/vnd.google-apps.folder' and trashed = false";
                if (!String.IsNullOrEmpty(parentId))
                {
                    query = $"{query} and '{parentId}' in parents";
                }

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        folders = new Dictionary<string, string>();
                        ListFilesResponse listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                        foreach (GoogleFile folder in listFilesResponse.Files)
                        {
                            folders.Add(folder.Id, folder.Name);
                            //Console.WriteLine($"ListFolders [{folder.Id}] = [{folder.Name}]");
                        }
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("ListFolders", parentId, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("ListFolders", parentId, $"List folders error. {parentId}", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("ListFolders", parentId, "Token error.");
            }

            return folders;
        }

        public async Task<ListFilesResponse> GetFolders()
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = "mimeType = 'application/vnd.google-apps.folder' and trashed = false";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("GetFolders", null, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("GetFolders", null, $"List folders error.", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("GetFolders", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<ListFilesResponse> ListImages()
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = "mimeType contains 'image'";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("ListImages", null, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("ListImages", null, $"Error", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("ListImages", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<ListFilesResponse> ListImagesInRootFolder()
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = $"mimeType contains 'image' and 'root' in parents";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                    }
                    else
                    {
                        _context.Vtex.Logger.Info("ListImagesInRootFolder", null, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("ListImagesInRootFolder", null, $"Error", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("ListImagesInRootFolder", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<ListFilesResponse> ListImagesInFolder(string folderId)
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = $"mimeType contains 'image' and '{folderId}' in parents";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                        _context.Vtex.Logger.Info("ListImagesInFolder", folderId, $"{listFilesResponse.Files.Count} files.  Complete list? {!listFilesResponse.IncompleteSearch}");
                    }
                    else
                    {
                        _context.Vtex.Logger.Warn("ListImagesInFolder", folderId, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("ListImagesInFolder", folderId, $"Error", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("ListImagesInFolder", folderId, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<string> CreateFolder(string folderName, string parentId = null)
        {
            string folderId = string.Empty;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                dynamic metadata = new JObject();
                metadata.name = folderName;
                metadata.mimeType = "application/vnd.google-apps.folder";
                if (!string.IsNullOrEmpty(parentId))
                {
                    JArray jarrayObj = new JArray();
                    jarrayObj.Add(parentId);
                    metadata.parents = jarrayObj;
                }

                var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}"),
                    Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                try
                {
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();
                    _context.Vtex.Logger.Info("CreateFolder", folderName, $"[{response.StatusCode}] {responseContent}");
                    if (response.IsSuccessStatusCode)
                    {
                        CreateFolderResponse createFolderResponse = JsonConvert.DeserializeObject<CreateFolderResponse>(responseContent);
                        folderId = createFolderResponse.Id;
                        Console.WriteLine($"CreateFolder {folderName} Id:{folderId} ParentId?{parentId}");
                    }
                }
                catch(Exception ex)
                {
                    _context.Vtex.Logger.Error("CreateFolder", folderName, $"Error. ParentId?{parentId}", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Info("CreateFolder", folderName, "Token error.");
            }

            return folderId;
        }

        public async Task<bool> MoveFile(string fileId, string folderId)
        {
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(folderId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    dynamic metadata = new JObject();
                    metadata.id = folderId;

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL_V2}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}/parents?enforceSingleParent=true"), // fields=*&q={query}
                        Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);
                        responseContent = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _context.Vtex.Logger.Info("MoveFile", null, $"[{response.StatusCode}] {responseContent}");
                        }

                        success = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("MoveFile", folderId, $"FileId {fileId}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("MoveFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Info("MoveFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<byte[]> GetFile(string fileId)
        {
            bool success = false;
            Stream contentStream = null;
            byte[] contentByteArray = null;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}?alt=media&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}")
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);

                        if (!response.IsSuccessStatusCode)
                        {
                            _context.Vtex.Logger.Info("GetFile", null, $"[{response.StatusCode}] {responseContent}");
                        }

                        contentStream = await response.Content.ReadAsStreamAsync();
                        contentByteArray = await response.Content.ReadAsByteArrayAsync();

                        success = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("GetFile", null, $"FileId {fileId}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("GetFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Info("GetFile", null, "Parameer missing.");
            }

            return contentByteArray;
        }

        public async Task<bool> SetPermission(string fileId)
        {
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    dynamic metadata = new JObject();
                    metadata.type = "anyone";
                    metadata.role = "reader";

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    // POST https://www.googleapis.com/drive/v3/files/fileId/permissions
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}/permissions"),
                        Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);
                        responseContent = await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            _context.Vtex.Logger.Info("SetPermission", null, $"[{response.StatusCode}] {responseContent}");
                        }

                        success = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("SetPermission", null, $"FileId {fileId}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("SetPermission", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Info("MoveFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<bool> RenameFile(string fileId, string fileName)
        {
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(fileName))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    dynamic metadata = new JObject();
                    metadata.title = fileName;

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Patch,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL_V2}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}"),
                        Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);
                        responseContent = await response.Content.ReadAsStringAsync();

                        success = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("RenameFile", null, $"FileId {fileId}, Filename '{fileName}'", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("RenameFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Info("RenameFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<bool> SaveFile(string fileName, StringBuilder fileContents)
        {
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileName))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    dynamic metadata = new JObject();
                    metadata.title = fileName;

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_UPLOAD_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}"),
                        Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);
                        responseContent = await response.Content.ReadAsStringAsync();

                        success = response.IsSuccessStatusCode;
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("SaveFile", null, $"Filename '{fileName}'", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("SaveFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Info("SaveFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<GoogleWatch> SetWatch(string fileId, bool reset = false)
        {
            Console.WriteLine("SetWatch");
            bool success = false;
            GoogleWatch googleWatchResponse = null;
            WatchExpiration watchExpiration = await _driveImportRepository.GetWatchExpiration(fileId);
            DateTime expiresAt = watchExpiration.ExpiresAt;
            int expirationWindowInHours = 1;
            Console.WriteLine($"expiresAt {expiresAt}  <  {DateTime.Now.AddHours(expirationWindowInHours)}");
            if (reset || expiresAt < DateTime.Now.AddHours(expirationWindowInHours))
            {
                string responseContent = string.Empty;
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    DateTime oneYear = DateTime.UtcNow.AddYears(1);
                    long unixTime = ((DateTimeOffset)oneYear).ToUnixTimeMilliseconds();
                    string siteUrl = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.FORWARDED_HOST];
                    GoogleWatch watchRequest = new GoogleWatch
                    {
                        //Kind = DriveImportConstants.WATCH_KIND,
                        Id = Guid.NewGuid().ToString(),
                        Type = DriveImportConstants.WATCH_TYPE,
                        Address = $"https://{siteUrl}/{DriveImportConstants.WATCH_ENDPOINT}",
                        Expiration = unixTime
                    };

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(watchRequest);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}/watch"),
                        Content = new StringContent(jsonSerializedMetadata, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    Console.WriteLine($"SetWatch '{request.RequestUri}' {jsonSerializedMetadata} {token.TokenType} {token.AccessToken}");

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    try
                    {
                        var response = await client.SendAsync(request);
                        if (response != null)
                        {
                            responseContent = await response.Content.ReadAsStringAsync();
                            //Console.WriteLine(responseContent);

                            if (!response.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"SetWatch '{response.StatusCode}' {responseContent}");
                                _context.Vtex.Logger.Info("SetWatch", null, $"[{response.StatusCode}] {responseContent}");
                            }
                            else
                            {
                                googleWatchResponse = JsonConvert.DeserializeObject<GoogleWatch>(responseContent);
                                long expiresIn = googleWatchResponse.Expiration ?? 0;
                                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(expiresIn);
                                expiresAt = dateTimeOffset.UtcDateTime;
                                watchExpiration = new WatchExpiration { ExpiresAt = expiresAt, FolderId = fileId };
                                await _driveImportRepository.SetWatchExpiration(watchExpiration);
                            }

                            success = response.IsSuccessStatusCode;
                        }
                        else
                        {
                            _context.Vtex.Logger.Info("SetWatch", null, $"Response is Null. FileId {fileId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("SetWatch", null, $"FileId {fileId}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Info("SetWatch", null, "Token error.");
                }
            }
            else
            {
                Console.WriteLine($"Watch will expire at {expiresAt}");
                _context.Vtex.Logger.Info("SetWatch", null, $"Watch will expire at {expiresAt}");
            }

            return googleWatchResponse;
        }

        public async Task<string> FindNewFolderId(string accountName)
        {
            string newFolderId = null;

            string importFolderId;
            string doneFolderId;
            string errorFolderId;
            string accountFolderId;
            string imagesFolderId;

            ListFilesResponse getFoldersResponse = await this.GetFolders();
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
                            if (!string.IsNullOrEmpty(newFolderId) && !string.IsNullOrEmpty(doneFolderId) && !string.IsNullOrEmpty(errorFolderId))
                            {
                                // Since we've done the work of looking these up, might as well save.
                                FolderIds folderIds = new FolderIds
                                {
                                    AccountFolderId = accountFolderId,
                                    DoneFolderId = doneFolderId,
                                    ErrorFolderId = errorFolderId,
                                    ImagesFolderId = imagesFolderId,
                                    ImportFolderId = imagesFolderId,
                                    NewFolderId = newFolderId
                                };

                                _context.Vtex.Logger.Info("FindNewFolderId", null, $"Saving Folder Ids: {JsonConvert.SerializeObject(folderIds)}");
                                await _driveImportRepository.SaveFolderIds(folderIds, accountName);
                            }
                        }
                    }
                }
            }

            _context.Vtex.Logger.Info("FindNewFolderId", null, $"New Fodler Id: {newFolderId}");

            return newFolderId;
        }
    }
}
