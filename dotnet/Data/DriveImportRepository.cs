namespace DriveImport.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using DriveImport.Models;
    using DriveImport.Services;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;

    public class DriveImportRepository : IDriveImportRepository
    {
        private readonly IVtexEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _applicationName;

        public DriveImportRepository(IVtexEnvironmentVariableProvider environmentVariableProvider, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory)
        {
            this._environmentVariableProvider = environmentVariableProvider ??
                                                throw new ArgumentNullException(nameof(environmentVariableProvider));

            this._httpContextAccessor = httpContextAccessor ??
                                        throw new ArgumentNullException(nameof(httpContextAccessor));

            this._clientFactory = clientFactory ??
                               throw new ArgumentNullException(nameof(clientFactory));

            this._applicationName =
                $"{this._environmentVariableProvider.ApplicationVendor}.{this._environmentVariableProvider.ApplicationName}";
        }


        public async Task<Credentials> GetCredentials()
        {
            //Console.WriteLine("-> GetCredentials <-");
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/master/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.CREDENTIALS}")
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

            //Console.WriteLine(responseContent);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            // A helper method is in order for this as it does not return the stack trace etc.
            response.EnsureSuccessStatusCode();

            Credentials credentials = JsonConvert.DeserializeObject<Credentials>(responseContent);
            return credentials;
        }

        public async Task SaveCredentials(Credentials credentials)
        {
            //Console.WriteLine("-> SaveCredentials <-");
            if (credentials == null)
            {
                Console.WriteLine("-> Credentials Null!!! <-");
                credentials = new Credentials();
            }

            var jsonSerializedCredentials = JsonConvert.SerializeObject(credentials);

            //Console.WriteLine(jsonSerializedCredentials);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/master/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.CREDENTIALS}"),
                Content = new StringContent(jsonSerializedCredentials, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
            };

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            //string responseContent = await response.Content.ReadAsStringAsync();
            //Console.WriteLine(responseContent);

            response.EnsureSuccessStatusCode();
        }

        public async Task<Token> LoadToken()
        {
            //Console.WriteLine("-> LoadToken <-");
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._environmentVariableProvider.Workspace}/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.TOKEN}")
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
            //Console.WriteLine($"-> LoadToken [{response.StatusCode}] {responseContent} <-");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            // A helper method is in order for this as it does not return the stack trace etc.
            response.EnsureSuccessStatusCode();

            Token token = JsonConvert.DeserializeObject<Token>(responseContent);

            return token;
        }

        public async Task<bool> SaveToken(Token token)
        {
            //Console.WriteLine("-> SaveToken <-");
            var jsonSerializedToken = JsonConvert.SerializeObject(token);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._environmentVariableProvider.Workspace}/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.TOKEN}"),
                Content = new StringContent(jsonSerializedToken, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
            };

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);
            //string responseContent = await response.Content.ReadAsStringAsync();
            //Console.WriteLine($"-> SaveToken [{response.StatusCode}] {responseContent} <-");
            return response.IsSuccessStatusCode;
        }

        public async Task<MerchantSettings> GetMerchantSettings()
        {
            // Load merchant settings
            // 'http://apps.${region}.vtex.io/${account}/${workspace}/apps/${vendor.appName}/settings'
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://apps.{this._environmentVariableProvider.Region}.vtex.io/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_WORKSPACE_HEADER_NAME]}/apps/{DriveImportConstants.APP_SETTINGS}/settings"),
            };

            //Console.WriteLine($"Request URL = {request.RequestUri}");

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();

            return JsonConvert.DeserializeObject<MerchantSettings>(responseContent);
        }

        public async Task SetImportLock(DateTime importStartTime)
        {
            var importLock = new Lock
            {
                ImportStarted = importStartTime,
            };

            var jsonSerializedLock = JsonConvert.SerializeObject(importLock);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_WORKSPACE_HEADER_NAME]}/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.LOCK}"),
                Content = new StringContent(jsonSerializedLock, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
            };

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }

        public async Task<DateTime> CheckImportLock()
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_WORKSPACE_HEADER_NAME]}/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.LOCK}")
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

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new DateTime();
            }

            // A helper method is in order for this as it does not return the stack trace etc.
            response.EnsureSuccessStatusCode();

            Lock importLock = JsonConvert.DeserializeObject<Lock>(responseContent);

            if (importLock.ImportStarted == null)
            {
                return new DateTime();
            }

            return importLock.ImportStarted;
        }

        public async Task ClearImportLock()
        {
            var importLock = new Lock
            {
                ImportStarted = new DateTime(),
            };

            var jsonSerializedLock = JsonConvert.SerializeObject(importLock);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_WORKSPACE_HEADER_NAME]}/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.LOCK}"),
                Content = new StringContent(jsonSerializedLock, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
            };

            string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
            if (authToken != null)
            {
                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
            }

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            response.EnsureSuccessStatusCode();
        }

        public async Task<WatchExpiration> GetWatchExpiration(string folderId)
        {
            IList<WatchExpiration> watchExpirationList = await this.GetWatchExpiration();
            WatchExpiration watchExpiration = watchExpirationList.Where(e => e.FolderId.Equals(folderId)).FirstOrDefault();
            if(watchExpiration == null)
            {
                watchExpiration = new WatchExpiration { ExpiresAt = DateTime.Now, FolderId = folderId };
            }

            return watchExpiration;
        }

        public async Task<IList<WatchExpiration>> GetWatchExpiration()
        {
            List<WatchExpiration> watchExpiration = new List<WatchExpiration>();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/master/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.WATCH_EXPIRATION}")
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

            Console.WriteLine($"GetWatchExpiration {responseContent}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            // A helper method is in order for this as it does not return the stack trace etc.
            response.EnsureSuccessStatusCode();
            try
            {
                watchExpiration = JsonConvert.DeserializeObject<List<WatchExpiration>>(responseContent);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error : {ex.Message}");
            }


            return watchExpiration;
        }

        public async Task SetWatchExpiration(WatchExpiration watchExpiration)
        {
            IList<WatchExpiration> watchExpirationList = await this.GetWatchExpiration();
            WatchExpiration watchExpirationTemp = watchExpirationList.Where(e => e.FolderId.Equals(watchExpiration.FolderId)).FirstOrDefault();
            if (watchExpirationTemp != null)
            {
                watchExpirationList.Remove(watchExpirationTemp);
            }

            watchExpirationList.Add(watchExpiration);

            var jsonSerializedExpiration = JsonConvert.SerializeObject(watchExpirationList);

            //Console.WriteLine(jsonSerializedCredentials);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri($"http://infra.io.vtex.com/vbase/v2/{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}/master/buckets/{this._applicationName}/{DriveImportConstants.BUCKET}/files/{DriveImportConstants.WATCH_EXPIRATION}"),
                Content = new StringContent(jsonSerializedExpiration, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
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
            Console.WriteLine($"SetWatchExpiration {responseContent}");

            response.EnsureSuccessStatusCode();
        }
    }
}
