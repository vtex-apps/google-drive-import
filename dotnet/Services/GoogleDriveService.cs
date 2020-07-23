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
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"REFRESHED Token = {responseContent}");
                if (response.IsSuccessStatusCode)
                {
                    tokenObj = JsonConvert.DeserializeObject<Token>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Info("RefreshGoogleAuthorizationToken", null, $"{response.StatusCode} {responseContent}");
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
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"RevokeGoogleAuthorizationToken = {responseContent}");
                if (response.IsSuccessStatusCode)
                {
                    success = true;
                }
                else
                {
                    _context.Vtex.Logger.Info("RevokeGoogleAuthorizationToken", null, $"{response.StatusCode} {responseContent}");
                }
            }

            return success;
        }

        public async Task<bool> ProcessReturn(string code)
        {
            Token token = await this.GetGoogleAuthorizationToken(code);
            token.ExpiresAt = DateTime.Now.AddSeconds(token.ExpiresIn);
            bool saved = await _driveImportRepository.SaveToken(token);
            return saved;
        }

        public async Task SaveCredentials(Credentials credentials)
        {
            await _driveImportRepository.SaveCredentials(credentials);
        }

        public async Task<Token> GetGoogleToken()
        {
            Token token = await _driveImportRepository.LoadToken();
            string refreshToken = token.RefreshToken;
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                if (token.ExpiresAt <= DateTime.Now)
                {
                    token = await this.RefreshGoogleAuthorizationToken(token.RefreshToken);
                    token.ExpiresAt = DateTime.Now.AddSeconds(token.ExpiresIn);
                    if(string.IsNullOrEmpty(token.RefreshToken))
                    {
                        token.RefreshToken = refreshToken;
                    }

                    bool saved = await _driveImportRepository.SaveToken(token);
                }
            }
            else
            {
                Console.WriteLine("Did not load token.");
                _context.Vtex.Logger.Info("GetGoogleToken", null, "Could not load token.");
            }

            return token;
        }

        public async Task<ListFilesResponse> ListFiles()
        {
            ListFilesResponse listFilesResponse = new ListFilesResponse();
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields=*"), // RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields=*&q=mimeType contains 'image'"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();

                if(response.IsSuccessStatusCode)
                {
                    listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                }
            }
            else
            {
                Console.WriteLine("Token error.");
                _context.Vtex.Logger.Info("ListFiles", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<Dictionary<string,string>> ListFolders()
        {
            Dictionary<string, string> folders = new Dictionary<string, string>();
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                string fields = "*";
                string query = "mimeType = 'application/vnd.google-apps.folder'";
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    ListFilesResponse listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                    foreach(GoogleFile folder in listFilesResponse.Files)
                    {
                        folders.Add(folder.Id, folder.Name);
                    }
                }
            }
            else
            {
                Console.WriteLine("Token error.");
                _context.Vtex.Logger.Info("ListFolders", null, "Token error.");
            }

            return folders;
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
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                }
            }
            else
            {
                Console.WriteLine("Token error.");
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
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();

                //Console.WriteLine($"ListImagesInFolder: [{response.StatusCode}] '{responseContent}'");

                if (response.IsSuccessStatusCode)
                {
                    listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                }
            }
            else
            {
                Console.WriteLine("Token error.");
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
                    RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}?fields={fields}&q={query}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();

                //Console.WriteLine($"ListImagesInFolder: [{response.StatusCode}] '{responseContent}'");

                if (response.IsSuccessStatusCode)
                {
                    listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                }
            }
            else
            {
                Console.WriteLine("Token error.");
                _context.Vtex.Logger.Info("ListImagesInFolder", null, "Token error.");
            }

            return listFilesResponse;
        }

        public async Task<bool> CreateFolder(string folderName)
        {
            bool success = false;
            string responseContent = string.Empty;
            Token token = await this.GetGoogleToken();
            if (token != null && !string.IsNullOrEmpty(token.AccessToken))
            {
                //string query = $"mimeType = 'application/vnd.google-apps.folder' and name = '{folderName}' and 'root' in parents";
                //StringBuilder postData = new StringBuilder();
                //postData.Append("mimeType=" + HttpUtility.UrlEncode("application/vnd.google-apps.folder") + "&");
                //postData.Append("name=" + HttpUtility.UrlEncode(folderName));

                dynamic metadata = new JObject();
                metadata.name = folderName;
                metadata.mimeType = "application/vnd.google-apps.folder";
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
                var response = await client.SendAsync(request);
                responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"CreateFolder '{folderName}': {responseContent}");
                success = response.IsSuccessStatusCode;
            }
            else
            {
                Console.WriteLine("Token error.");
                _context.Vtex.Logger.Info("CreateFolder", null, "Token error.");
            }

            return success;
        }

        public async Task<bool> MoveFile(string fileId, string folderId)
        {
            //Console.WriteLine($"Moving {fileId} to folder {folderId}");
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(folderId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    //List<string> parents = new List<string> { folderId };
                    dynamic metadata = new JObject();
                    //metadata.parents = JToken.FromObject(parents);
                    metadata.id = folderId;

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    Console.WriteLine(jsonSerializedMetadata);

                    //string query = $"addParents '{folderId}' and removeParents 'root'";

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
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    //Console.WriteLine($"MoveFile {response.StatusCode} {responseContent}");
                    Console.WriteLine($"MoveFile {response.StatusCode}");

                    success = response.IsSuccessStatusCode;
                }
                else
                {
                    Console.WriteLine("Token error.");
                    _context.Vtex.Logger.Info("MoveFile", null, "Token error.");
                }
            }
            else
            {
                Console.WriteLine("Parameter missing.");
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
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}?alt=media")
                    };

                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, $"{token.TokenType} {token.AccessToken}");

                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);
                    //responseContent = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"GetFile {response.StatusCode}");
                    //foreach (var header in response.Headers)
                    //{
                    //    Console.WriteLine($"GetFile Header [{ header.Key} : { header.Value.FirstOrDefault()}]");
                    //}

                    //foreach (var header in response.Content.Headers)
                    //{
                    //    Console.WriteLine($"GetFile Content [{ header.Key} : { header.Value.FirstOrDefault()}]");
                    //}

                    contentStream = await response.Content.ReadAsStreamAsync();
                    contentByteArray = await response.Content.ReadAsByteArrayAsync();

                    success = response.IsSuccessStatusCode;
                }
                else
                {
                    Console.WriteLine("Token error.");
                    _context.Vtex.Logger.Info("GetFile", null, "Token error.");
                }
            }
            else
            {
                Console.WriteLine("Parameer missing.");
                _context.Vtex.Logger.Info("GetFile", null, "Parameer missing.");
            }

            return contentByteArray;
        }

        public async Task<bool> SetPermission(string fileId)
        {
            //Console.WriteLine($"Moving {fileId} to folder {folderId}");
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    //List<string> parents = new List<string> { folderId };
                    dynamic metadata = new JObject();
                    //metadata.parents = JToken.FromObject(parents);
                    metadata.type = "anyone";
                    metadata.role = "reader";

                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    Console.WriteLine(jsonSerializedMetadata);

                    //string query = $"addParents '{folderId}' and removeParents 'root'";

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
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    Console.WriteLine($"SetPermission {response.StatusCode} {responseContent}");

                    success = response.IsSuccessStatusCode;
                }
                else
                {
                    Console.WriteLine("Token error.");
                    _context.Vtex.Logger.Info("SetPermission", null, "Token error.");
                }
            }
            else
            {
                Console.WriteLine("Parameter missing.");
                _context.Vtex.Logger.Info("MoveFile", null, "Parameter missing.");
            }

            return success;
        }

        public async Task<bool> RenameFile(string fileId, string fileName)
        {
            //Console.WriteLine($"Moving {fileId} to folder {folderId}");
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

                    Console.WriteLine(jsonSerializedMetadata);

                    //string query = $"addParents '{folderId}' and removeParents 'root'";

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
                    var response = await client.SendAsync(request);
                    responseContent = await response.Content.ReadAsStringAsync();

                    //Console.WriteLine($"RenameFile {response.StatusCode} {responseContent}");
                    Console.WriteLine($"RenameFile {response.StatusCode}");

                    success = response.IsSuccessStatusCode;
                }
                else
                {
                    Console.WriteLine("Token error.");
                    _context.Vtex.Logger.Info("RenameFile", null, "Token error.");
                }
            }
            else
            {
                Console.WriteLine("Parameter missing.");
                _context.Vtex.Logger.Info("RenameFile", null, "Parameter missing.");
            }

            return success;
        }
    }
}
