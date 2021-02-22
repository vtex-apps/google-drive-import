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
            Token tokenObj = null; // new Token();
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
                        if (token != null)
                        {
                            token.ExpiresAt = DateTime.Now.AddSeconds(token.ExpiresIn);
                            if (string.IsNullOrEmpty(token.RefreshToken))
                            {
                                token.RefreshToken = refreshToken;
                            }

                            bool saved = await _driveImportRepository.SaveToken(token);
                        }
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
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}&orderBy=name&pageSize={DriveImportConstants.GOOGLE_DRIVE_PAGE_SIZE}"),
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
                _context.Vtex.Logger.Warn("ListImagesInFolder", folderId, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<ListFilesResponse> ListSheetsInFolder(string folderId)
        {
            ListFilesResponse listFilesResponse = null;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = $"mimeType contains 'spreadsheet' and '{folderId}' in parents and trashed = false";
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
                        _context.Vtex.Logger.Info("ListSheetsInFolder", folderId, $"{listFilesResponse.Files.Count} files.  Complete list? {!listFilesResponse.IncompleteSearch}");
                    }
                    else
                    {
                        _context.Vtex.Logger.Warn("ListSheetsInFolder", folderId, $"[{response.StatusCode}] {responseContent}");
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("ListSheetsInFolder", folderId, $"Error", ex);
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("ListSheetsInFolder", folderId, "Token error.");
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
                    _context.Vtex.Logger.Warn("MoveFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("MoveFile", null, "Parameter missing.");
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
                    _context.Vtex.Logger.Warn("GetFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("GetFile", null, "Parameter missing.");
            }

            return contentByteArray;
        }

        public async Task<string> GetSheet(string fileId, string range)
        {
            bool success = false;
            string responseContent = string.Empty;
            if(string.IsNullOrEmpty(range))
            {
                range = "A:Z";
            }

            if (!string.IsNullOrEmpty(fileId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_SHEET_URL}/{DriveImportConstants.GOOGLE_DRIVE_SHEETS}/{fileId}/values:batchGet?ranges={range}")
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
                            _context.Vtex.Logger.Info("GetSheet", null, $"FileId:{fileId} [{response.StatusCode}] '{responseContent}'");
                        }

                        success = response.IsSuccessStatusCode;
                        Console.WriteLine($"    -   GetSheet responseStatus = '{response.StatusCode}'");
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("GetSheet", null, $"FileId {fileId}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSheet", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("GetSheet", null, "Parameter missing.");
            }

            //Console.WriteLine($"    -   GetSheet responseContent = '{responseContent}'");
            return responseContent;
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
                    _context.Vtex.Logger.Warn("SetPermission", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("MoveFile", null, "Parameter missing.");
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
                    _context.Vtex.Logger.Warn("RenameFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("RenameFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<string> SaveFile(StringBuilder file)
        {
            string fileId = string.Empty;
            CreateFolderResponse createResponse = null;
            string responseContent = string.Empty;
            if (file != null)
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_UPLOAD_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?uploadType=media&supportsAllDrives=true"),
                        Content = new StringContent(file.ToString(), Encoding.UTF8, DriveImportConstants.TEXT)
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
                        Console.WriteLine($"[|]     SaveFile {responseContent}");

                        if(response.IsSuccessStatusCode)
                        {
                            createResponse = JsonConvert.DeserializeObject<CreateFolderResponse>(responseContent);
                            fileId = createResponse.Id;
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("SaveFile", null, "Error saving file", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("SaveFile", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("SaveFile", null, "Parameter missing.");
            }

            return fileId;
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

        public async Task<string> CreateSpreadsheet(GoogleSheetCreate googleSheetRequest)
        {
            bool success = false;
            string responseContent = string.Empty;
            string fileId = string.Empty;
            GoogleSheetCreate googleSheetResponse;

            if (googleSheetRequest != null)
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var jsonSerializedMetadata = JsonConvert.SerializeObject(googleSheetRequest);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_SHEET_URL}/{DriveImportConstants.GOOGLE_DRIVE_SHEETS}"),
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
                        Console.WriteLine($"CreateSpreadsheet [{response.StatusCode}] {responseContent}");
                        success = response.IsSuccessStatusCode;
                        if(success)
                        {
                            googleSheetResponse = JsonConvert.DeserializeObject<GoogleSheetCreate>(responseContent);
                            fileId = googleSheetResponse.SpreadsheetId;
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("CreateSpreadsheet", null, $"{jsonSerializedMetadata}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("CreateSpreadsheet", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("CreateSpreadsheet", null, "Parameter missing.");
            }

            return fileId;
        }

        public async Task<UpdateValuesResponse> WriteSpreadsheetValues(string fileId, ValueRange valueRange)
        {
            string responseContent = string.Empty;
            UpdateValuesResponse updateValuesResponse = null;

            if (!string.IsNullOrEmpty(fileId) && valueRange != null)
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var jsonSerializedMetadata = JsonConvert.SerializeObject(valueRange);
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Put,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_SHEET_URL}/{DriveImportConstants.GOOGLE_DRIVE_SHEETS}/{fileId}/values/{valueRange.Range}?valueInputOption=USER_ENTERED"),
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
                        Console.WriteLine($"WriteSpreadsheetValues [{response.StatusCode}] {responseContent}");
                        if (response.IsSuccessStatusCode)
                        {
                            updateValuesResponse = JsonConvert.DeserializeObject<UpdateValuesResponse>(responseContent);
                        }
                        else
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                _context.Vtex.Logger.Warn("WriteSpreadsheetValues", null, $"Retrying [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                await Task.Delay(1000 * 60);
                                client = _clientFactory.CreateClient();
                                response = await client.SendAsync(request);
                                if (response.IsSuccessStatusCode)
                                {
                                    responseContent = await response.Content.ReadAsStringAsync();
                                    updateValuesResponse = JsonConvert.DeserializeObject<UpdateValuesResponse>(responseContent);
                                }
                                else
                                {
                                    _context.Vtex.Logger.Error("WriteSpreadsheetValues", null, $"Did not update sheet [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                }
                            }
                            else
                            {
                                _context.Vtex.Logger.Error("WriteSpreadsheetValues", null, $"[{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("WriteSpreadsheetValues", null, $"{jsonSerializedMetadata}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("WriteSpreadsheetValues", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("WriteSpreadsheetValues", null, "Parameter missing.");
            }

            return updateValuesResponse;
        }

        public async Task<string> UpdateSpreadsheet(string fileId, BatchUpdate batchUpdate)
        {
            string responseContent = string.Empty;

            if (batchUpdate != null)
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var jsonSerializedMetadata = JsonConvert.SerializeObject(batchUpdate);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_SHEET_URL}/{DriveImportConstants.GOOGLE_DRIVE_SHEETS}/{fileId}:batchUpdate"),
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
                        //Console.WriteLine($"UpdateSpreadsheet [{response.StatusCode}] {responseContent}");
                        if (!response.IsSuccessStatusCode)
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                _context.Vtex.Logger.Warn("UpdateSpreadsheet", null, $"Retrying [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                await Task.Delay(1000 * 60);
                                client = _clientFactory.CreateClient();
                                response = await client.SendAsync(request);
                                if (response.IsSuccessStatusCode)
                                {
                                    responseContent = await response.Content.ReadAsStringAsync();
                                }
                                else
                                {
                                    _context.Vtex.Logger.Error("UpdateSpreadsheet", null, $"Did not update sheet [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                }
                            }
                            else
                            {
                                _context.Vtex.Logger.Warn("UpdateSpreadsheet", null, $"Did not update sheet. [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("UpdateSpreadsheet", null, $"{jsonSerializedMetadata}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("UpdateSpreadsheet", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("UpdateSpreadsheet", null, "Parameter missing.");
            }

            return responseContent;
        }

        public async Task<string> ClearSpreadsheet(string fileId, SheetRange sheetRange)
        {
            string responseContent = string.Empty;

            if (sheetRange != null)
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    var jsonSerializedMetadata = JsonConvert.SerializeObject(sheetRange);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_SHEET_URL}/{DriveImportConstants.GOOGLE_DRIVE_SHEETS}/{fileId}/values:batchClear"),
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
                        Console.WriteLine($"ClearSpreadsheet [{response.StatusCode}] {responseContent}");
                        if (!response.IsSuccessStatusCode)
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                            {
                                _context.Vtex.Logger.Warn("ClearSpreadsheet", null, $"Retrying [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                await Task.Delay(1000 * 60);
                                client = _clientFactory.CreateClient();
                                response = await client.SendAsync(request);
                                if (response.IsSuccessStatusCode)
                                {
                                    responseContent = await response.Content.ReadAsStringAsync();
                                }
                                else
                                {
                                    _context.Vtex.Logger.Error("ClearSpreadsheet", null, $"Did not clear sheet [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                                }
                            }
                            else
                            {
                                _context.Vtex.Logger.Warn("ClearSpreadsheet", null, $"Did not clear sheet. [{response.StatusCode}] {responseContent} {jsonSerializedMetadata}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _context.Vtex.Logger.Error("ClearSpreadsheet", null, $"{jsonSerializedMetadata}", ex);
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("ClearSpreadsheet", null, "Token error.");
                }
            }
            else
            {
                _context.Vtex.Logger.Warn("ClearSpreadsheet", null, "Parameter missing.");
            }

            return responseContent;
        }

        public async Task<string> AddImagesToSheet()
        {
            string result = string.Empty;
            string newFolderId = null;
            string imagesFolderId = null;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                newFolderId = folderIds.NewFolderId;
                imagesFolderId = folderIds.ImagesFolderId;

                ListFilesResponse imageFiles = await this.ListImagesInFolder(newFolderId);
                ListFilesResponse spreadsheets = await this.ListSheetsInFolder(imagesFolderId);

                if (imageFiles != null && spreadsheets != null)
                {


                    var sheetIds = spreadsheets.Files.Select(s => s.Id);
                    if (sheetIds != null)
                    {
                        foreach (var sheetId in sheetIds)
                        {
                            Dictionary<string, int> headerIndexDictionary = new Dictionary<string, int>();
                            Dictionary<string, string> columns = new Dictionary<string, string>();
                            string sheetContent = await this.GetSheet(sheetId, string.Empty);

                            GoogleSheet googleSheet = JsonConvert.DeserializeObject<GoogleSheet>(sheetContent);
                            string valueRange = googleSheet.ValueRanges[0].Range;
                            string sheetName = valueRange.Split("!")[0];
                            string[] sheetHeader = googleSheet.ValueRanges[0].Values[0];
                            int headerIndex = 0;
                            int rowCount = googleSheet.ValueRanges[0].Values.Count();
                            int writeBlockSize = imageFiles.Files.Count;
                            foreach (string header in sheetHeader)
                            {
                                headerIndexDictionary.Add(header.ToLower(), headerIndex);
                                headerIndex++;
                            }

                            int imageColumnNumber = headerIndexDictionary["image"] + 65;
                            string imageColumnLetter = ((char)imageColumnNumber).ToString();
                            int thumbnailColumnNumber = headerIndexDictionary["thumbnail"] + 65;
                            string thumbnailColumnLetter = ((char)thumbnailColumnNumber).ToString();

                            //string[][] arrValuesToWrite = new string[rowCount][];
                            string[][] arrValuesToWrite = new string[writeBlockSize][];
                            int index = 0;
                            foreach (GoogleFile file in imageFiles.Files)
                            {
                                if (file.Name.Contains(","))
                                {
                                    // skipping old format
                                }
                                else
                                {
                                    string[] row = new string[headerIndexDictionary.Count];
                                    row[headerIndexDictionary["image"]] = file.Name;
                                    row[headerIndexDictionary["thumbnail"]] = $"=IMAGE(\"{ file.ThumbnailLink}\")";
                                    arrValuesToWrite[index] = row;
                                    index++;
                                }
                            }

                            string lastColumn = ((char)(headerIndexDictionary.Count + 65)).ToString();
                            ValueRange valueRangeToWrite = new ValueRange
                            {
                                //Range = $"{sheetName}!{imageColumnLetter}:{thumbnailColumnLetter}",
                                Range = $"{sheetName}!A2:{lastColumn}{index + 2}",
                                Values = arrValuesToWrite
                            };

                            var writeToSheetResult = await this.WriteSpreadsheetValues(sheetId, valueRangeToWrite);
                        }
                    }
                }
            }

            return result;
        }

        public async Task<string> CreateSheet()
        {
            string sheetUrl = string.Empty;
            string sheetName = "VtexImageImport";
            string sheetLabel = "ImagesForImport";
            string instructionsLabel = "Instructions";
            string[] headerRowLabels = new string[]
                {
                    "Image","Thumbnail","Type","Value","Name","Label","Main","Attributes","Status","Message","Date"
                };

            int headerIndex = 0;
            Dictionary<string, int> headerIndexDictionary = new Dictionary<string, int>();
            foreach (string header in headerRowLabels)
            {
                //Console.WriteLine($"({headerIndex}) sheetHeader = {header}");
                headerIndexDictionary.Add(header.ToLower(), headerIndex);
                headerIndex++;
            }

            int statusRow = headerIndexDictionary["status"];
            int typeRow = headerIndexDictionary["type"];

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
                                RowCount = DriveImportConstants.DEFAULT_SHEET_SIZE
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
                                        EndColumnIndex = statusRow + 1,
                                        EndRowIndex = DriveImportConstants.DEFAULT_SHEET_SIZE,
                                        StartColumnIndex = statusRow,
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
                                        EndColumnIndex = statusRow + 1,
                                        EndRowIndex = DriveImportConstants.DEFAULT_SHEET_SIZE,
                                        StartColumnIndex = statusRow,
                                        StartRowIndex = 1
                                    }
                                }
                            },
                        }
                    },
                    new Sheet
                    {
                        Properties = new SheetProperties
                        {
                            SheetId = 1,
                            Title = instructionsLabel,
                            Index = 1,
                            GridProperties = new GridProperties
                            {
                                ColumnCount = 4,
                                RowCount = 8
                            },
                            SheetType = "GRID"
                        }
                    }
                }
            };

            string sheetId = await this.CreateSpreadsheet(googleSheetCreate);

            if (!string.IsNullOrEmpty(sheetId))
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

                UpdateValuesResponse updateValuesResponse = await this.WriteSpreadsheetValues(sheetId, valueRange);

                valueRange = new ValueRange
                {
                    MajorDimension = "ROWS",
                    Range = $"{instructionsLabel}!A1:D8",
                    Values = new string[][]
                    {
                        new string[] { "Populate the following fields", "", "Example", "Notes" },
                        new string[] { "Image", "Image file name", "shirt1.jpg","Use DELETE to remove images for the Type/Value" },
                        new string[] { "Type", "The identifier for the Value field", "SkuId, SkuRefId, ProductId, or ProductRefId","" },
                        new string[] { "Value", "The value for the Type", "83", "" },
                        new string[] { "Name", "Image name", "shirt-front", "" },
                        new string[] { "Label", "Image label", "Shirt Front", "" },
                        new string[] { "Main", "Set the image as the Main image", "true","Any text will set the image as Main" },
                        new string[] { "Attributes", "Optionally limit by aku attribute", "color=red", "" }
                    }
                };

                updateValuesResponse = await this.WriteSpreadsheetValues(sheetId, valueRange);

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
                        },
                        new Request
                        {
                            SetDataValidation = new SetDataValidation
                            {
                                Range = new BatchUpdateRange
                                {
                                    StartRowIndex = 1,
                                    EndRowIndex = DriveImportConstants.DEFAULT_SHEET_SIZE,
                                    SheetId = 0,
                                    EndColumnIndex = typeRow + 1,
                                    StartColumnIndex = typeRow
                                },
                                Rule = new Rule
                                {
                                    Condition = new Condition
                                    {
                                        Type = "ONE_OF_LIST",
                                        Values = new Value[]
                                        {
                                            new Value
                                            {
                                                UserEnteredValue = string.Empty
                                            },
                                            new Value
                                            {
                                                UserEnteredValue = DriveImportConstants.IdentificatorType.SKU_ID
                                            },
                                            new Value
                                            {
                                                UserEnteredValue = DriveImportConstants.IdentificatorType.SKU_REF_ID
                                            },
                                            new Value
                                            {
                                                UserEnteredValue = DriveImportConstants.IdentificatorType.PRODUCT_ID
                                            },
                                            new Value
                                            {
                                                UserEnteredValue = DriveImportConstants.IdentificatorType.PRODUCT_REF_ID
                                            }
                                        }
                                    },
                                    InputMessage = $"Valid values: '{DriveImportConstants.IdentificatorType.SKU_ID}', '{DriveImportConstants.IdentificatorType.SKU_REF_ID}', '{DriveImportConstants.IdentificatorType.PRODUCT_ID}', '{DriveImportConstants.IdentificatorType.PRODUCT_REF_ID}'",
                                    Strict = true
                                }
                            }
                        },
                        new Request
                        {
                            AutoResizeDimensions = new AutoResizeDimensions
                            {
                                Dimensions = new Dimensions
                                {
                                    Dimension = "COLUMNS",
                                    EndIndex = 4,
                                    StartIndex = 0,
                                    SheetId = 1
                                }
                            }
                        },
                        //new Request
                        //{
                        //    AutoResizeDimensions = new AutoResizeDimensions
                        //    {
                        //        Dimensions = new Dimensions
                        //        {
                        //            Dimension = "COLUMNS",
                        //            EndIndex = headerRowLabels.Count(),
                        //            StartIndex = 0,
                        //            SheetId = 0
                        //        }
                        //    }
                        //}
                    }
                };

                var updateSheet = await this.UpdateSpreadsheet(sheetId, batchUpdate);
                //Console.WriteLine($"updateSheet = {updateSheet}");

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
                    Dictionary<string, string> folders = await this.ListFolders();   // Id, Name

                    if (folders == null)
                    {
                        return ($"Error accessing Drive.");
                    }

                    if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMPORT))
                    {
                        importFolderId = await this.CreateFolder(DriveImportConstants.FolderNames.IMPORT);
                    }
                    else
                    {
                        importFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMPORT).Key;
                    }

                    if (string.IsNullOrEmpty(importFolderId))
                    {
                        _context.Vtex.Logger.Info("SheetImport", null, $"Could not find '{DriveImportConstants.FolderNames.IMPORT}' folder");
                        return ($"Could not find {DriveImportConstants.FolderNames.IMPORT} folder");
                    }

                    folders = await this.ListFolders(importFolderId);

                    if (!folders.ContainsValue(accountName))
                    {
                        accountFolderId = await this.CreateFolder(accountName, importFolderId);
                    }
                    else
                    {
                        accountFolderId = folders.FirstOrDefault(x => x.Value == accountName).Key;
                    }

                    if (string.IsNullOrEmpty(accountFolderId))
                    {
                        _context.Vtex.Logger.Info("SheetImport", null, $"Could not find {accountFolderId} folder");
                        return ($"Could not find {accountFolderId} folder");
                    }

                    folders = await this.ListFolders(accountFolderId);

                    if (!folders.ContainsValue(DriveImportConstants.FolderNames.IMAGES))
                    {
                        imagesFolderId = await this.CreateFolder(DriveImportConstants.FolderNames.IMAGES, accountFolderId);
                    }
                    else
                    {
                        imagesFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.IMAGES).Key;
                    }

                    if (string.IsNullOrEmpty(imagesFolderId))
                    {
                        _context.Vtex.Logger.Info("SheetImport", null, $"Could not find {imagesFolderId} folder");
                        return ($"Could not find {imagesFolderId} folder");
                    }

                    folders = await this.ListFolders(imagesFolderId);

                    if (!folders.ContainsValue(DriveImportConstants.FolderNames.NEW))
                    {
                        newFolderId = await this.CreateFolder(DriveImportConstants.FolderNames.NEW, imagesFolderId);
                        bool setPermission = await this.SetPermission(newFolderId);
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
                        doneFolderId = await this.CreateFolder(DriveImportConstants.FolderNames.DONE, imagesFolderId);
                    }
                    else
                    {
                        doneFolderId = folders.FirstOrDefault(x => x.Value == DriveImportConstants.FolderNames.DONE).Key;
                    }

                    if (!folders.ContainsValue(DriveImportConstants.FolderNames.ERROR))
                    {
                        errorFolderId = await this.CreateFolder(DriveImportConstants.FolderNames.ERROR, imagesFolderId);
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

                bool moved = await this.MoveFile(sheetId, imagesFolderId);
                Console.WriteLine($"Moved? {moved}");
                if (moved)
                {
                    await this.AddImagesToSheet();
                    batchUpdate = new BatchUpdate
                    {
                        Requests = new Request[]
                        {
                            new Request
                            {
                                AutoResizeDimensions = new AutoResizeDimensions
                                {
                                    Dimensions = new Dimensions
                                    {
                                        Dimension = "COLUMNS",
                                        EndIndex = 2, //headerRowLabels.Count(),
                                        StartIndex = 0,
                                        SheetId = 0
                                    }
                                }
                            }
                        }
                    };

                    updateSheet = await this.UpdateSpreadsheet(sheetId, batchUpdate);
                }
            }

            string result = string.IsNullOrEmpty(sheetId) ? "Error" : "Created";
            return (await this.GetSheetLink());
        }

        public async Task<string> GetSheetLink()
        {
            string sheetUrl = string.Empty;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];

            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                string imagesFolderId = folderIds.ImagesFolderId;
                ListFilesResponse spreadsheets = await this.ListSheetsInFolder(imagesFolderId);
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

            return (sheetUrl);
        }

        public async Task ClearAndAddImages()
        {
            string response = string.Empty;
            string imagesFolderId = null;
            string accountName = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME];
            FolderIds folderIds = await _driveImportRepository.LoadFolderIds(accountName);
            if (folderIds != null)
            {
                imagesFolderId = folderIds.ImagesFolderId;
                ListFilesResponse spreadsheets = await this.ListSheetsInFolder(imagesFolderId);
                if (spreadsheets != null)
                {
                    SheetRange sheetRange = new SheetRange();
                    sheetRange.Ranges = new List<string>();
                    sheetRange.Ranges.Add($"A2:Z{DriveImportConstants.DEFAULT_SHEET_SIZE}");

                    foreach (GoogleFile sheet in spreadsheets.Files)
                    {
                        response = response + " - " + await this.ClearSpreadsheet(sheet.Id, sheetRange);
                    }
                }
                else
                {
                    response = "null sheet";
                }
            }
            else
            {
                response = "null folderIds";
            }

            response = response + " - " + await this.AddImagesToSheet();
        }
    }
}
