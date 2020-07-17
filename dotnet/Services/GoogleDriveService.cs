using DriveImport.Data;
using DriveImport.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DriveImport.Services
{
    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly IVtexEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IDriveImportRepository _driveImportRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _applicationName;

        public GoogleDriveService(IVtexEnvironmentVariableProvider environmentVariableProvider, IDriveImportRepository driveImportRepository, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory)
        {
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
            if (credentials != null && !string.IsNullOrEmpty(credentials.Web.ClientId))
            {
                string redirectUri = $"{DriveImportConstants.REDIRECT_SITE_BASE}/{DriveImportConstants.APP_NAME}/{DriveImportConstants.REDIRECT_PATH}/";
                string clientId = credentials.Web.ClientId;
                authUrl = $"{credentials.Web.AuthUri}?scope={DriveImportConstants.GOOGLE_SCOPE}&response_type={DriveImportConstants.GOOGLE_REPONSE_TYPE}&access_type={DriveImportConstants.GOOGLE_ACCESS_TYPE}&redirect_uri={redirectUri}&client_id={clientId}";
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

            return tokenObj;
        }

        public async Task<Token> RefreshGoogleAuthorizationToken(string refreshToken)
        {
            Token tokenObj = new Token();
            if (string.IsNullOrEmpty(refreshToken))
            {
                Console.WriteLine("Refresh Token Empty");
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
            }

            return tokenObj;
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
            }

            return token;
        }

        public async Task<string> ListFiles()
        {
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
                    ListFilesResponse listFilesResponse = JsonConvert.DeserializeObject<ListFilesResponse>(responseContent);
                }
            }
            else
            {
                Console.WriteLine("Token error.");
            }

            return responseContent;
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
                    foreach(File folder in listFilesResponse.Files)
                    {
                        folders.Add(folder.Id, folder.Name);
                    }
                }
            }
            else
            {
                Console.WriteLine("Token error.");
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
            }

            return success;
        }

        public async Task<bool> MoveFile(string fileId, string folderId)
        {
            Console.WriteLine($"Moving {fileId} to folder {folderId}");
            bool success = false;
            string responseContent = string.Empty;
            if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(folderId))
            {
                Token token = await this.GetGoogleToken();
                if (token != null && !string.IsNullOrEmpty(token.AccessToken))
                {
                    List<string> parents = new List<string> { folderId };
                    dynamic metadata = new JObject();
                    metadata.parents = JToken.FromObject(parents);
                    var jsonSerializedMetadata = JsonConvert.SerializeObject(metadata);

                    Console.WriteLine(jsonSerializedMetadata);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Patch,
                        RequestUri = new Uri($"{DriveImportConstants.GOOGLE_DRIVE_URL}/{DriveImportConstants.GOOGLE_DRIVE_FILES}/{fileId}"),
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

                    Console.WriteLine($"MoveFile {response.StatusCode} {responseContent}");

                    success = response.IsSuccessStatusCode;
                }
                else
                {
                    Console.WriteLine("Token error.");
                }
            }
            else
            {
                Console.WriteLine("Parameer missing.");
            }

            return success;
        }
    }
}
