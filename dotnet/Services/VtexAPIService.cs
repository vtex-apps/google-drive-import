using DriveImport.Data;
using DriveImport.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vtex.Api.Context;

namespace DriveImport.Services
{
    public class VtexAPIService : IVtexAPIService
    {
        private readonly IIOServiceContext _context;
        private readonly IVtexEnvironmentVariableProvider _environmentVariableProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _applicationName;

        public VtexAPIService(IIOServiceContext context, IVtexEnvironmentVariableProvider environmentVariableProvider, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory)
        {
            this._context = context ??
                            throw new ArgumentNullException(nameof(context));

            this._environmentVariableProvider = environmentVariableProvider ??
                                                throw new ArgumentNullException(nameof(environmentVariableProvider));

            this._httpContextAccessor = httpContextAccessor ??
                                        throw new ArgumentNullException(nameof(httpContextAccessor));

            this._clientFactory = clientFactory ??
                               throw new ArgumentNullException(nameof(clientFactory));

            this._applicationName =
                $"{this._environmentVariableProvider.ApplicationVendor}.{this._environmentVariableProvider.ApplicationName}";
        }

        public async Task<bool> UpdateSkuImage(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl)
        {
            Console.WriteLine($"UpdateSkuImage '{skuId}' {imageName}");

            //POST https://{{accountName}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/file
            //    {
            //                    "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //     "Url": "https://images2.alphacoders.com/509/509945.jpg"
            //    }

            bool success = false;

            try
            {
                ImageUpdate imageUpdate = new ImageUpdate
                {
                    IsMain = isMain,
                    Label = imageLabel,
                    Name = imageName,
                    Text = null,
                    Url = imageUrl
                };

                string jsonSerializedData = JsonConvert.SerializeObject(imageUpdate);

                Console.WriteLine($"jsonSerializedData = {jsonSerializedData}");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                    Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                //request.Headers.Add(Constants.ACCEPT, Constants.APPLICATION_JSON);
                //request.Headers.Add(Constants.CONTENT_TYPE, Constants.APPLICATION_JSON);
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                //Console.WriteLine($"Token = '{authToken}'");
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"UpdateSkuImage Response: {response.StatusCode} {responseContent}");

                success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateSkuImage Error: {ex.Message}");
                _context.Vtex.Logger.Error("UpdateSkuImage", null, $"Error updating sku '{skuId}' {imageName}", ex);
            }

            return success;
        }

        public async Task<bool> UpdateSkuImageByFormData(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl)
        {
            Console.WriteLine($"UpdateSkuImageByFormData '{skuId}' {imageName}");

            //POST https://{{accountName}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/file
            //    {
            //                    "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //     "Url": "https://images2.alphacoders.com/509/509945.jpg"
            //    }

            bool success = false;

            try
            {
                //MultipartFormDataContent form = new MultipartFormDataContent();
                //form.Add(new ByteArrayContent(file_bytes, 0, file_bytes.Length), "profile_pic", "hello1.jpg");

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                    //Content = new StreamContent(new MemoryStream(form)), "bilddatei", "upload.jpg" };
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                //request.Headers.Add(Constants.ACCEPT, Constants.APPLICATION_JSON);
                //request.Headers.Add(Constants.CONTENT_TYPE, Constants.APPLICATION_JSON);
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                //Console.WriteLine($"Token = '{authToken}'");
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"UpdateSkuImageByFormData Response: {response.StatusCode} {responseContent}");

                success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateSkuImageByFormData Error: {ex.Message}");
                _context.Vtex.Logger.Error("UpdateSkuImageByFormData", null, $"Error updating sku '{skuId}' {imageName}", ex);
            }

            return success;
        }

        public async Task<string> GetSkuIdFromReference(string skuRefId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/sku/stockkeepingunitidbyrefid/{{refId}}

            string skuId = string.Empty;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/sku/stockkeepingunitidbyrefid/{skuRefId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                //request.Headers.Add(Constants.ACCEPT, Constants.APPLICATION_JSON);
                //request.Headers.Add(Constants.CONTENT_TYPE, Constants.APPLICATION_JSON);
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                //Console.WriteLine($"Token = '{authToken}'");
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    skuId = responseContent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSkuIdFromReference Error: {ex.Message}");
                _context.Vtex.Logger.Error("GetSkuIdFromReference", null, $"Error getting sku for reference id '{skuRefId}'", ex);
            }

            return skuId;
        }

        public async Task<string> GetProductIdFromReference(string productRefId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/products/productgetbyrefid/{{refId}}

            string productId = string.Empty;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/products/productgetbyrefid/{productRefId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                //request.Headers.Add(Constants.ACCEPT, Constants.APPLICATION_JSON);
                //request.Headers.Add(Constants.CONTENT_TYPE, Constants.APPLICATION_JSON);
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                //Console.WriteLine($"Token = '{authToken}'");
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    productId = responseContent;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetProductIdFromReference Error: {ex.Message}");
                _context.Vtex.Logger.Error("GetProductIdFromReference", null, $"Error getting product id for reference '{productRefId}'", ex);
            }

            return productId;
        }

        public async Task<List<string>> GetSkusFromProductId(string productId)
        {
            // GET https://{{accountName}}.vtexcommercestable.com.br/api/catalog_system/pvt/sku/stockkeepingunitByProductId/{{productId}}

            List<string> skuIds = new List<string>();

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/sku/stockkeepingunitByProductId/{productId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                //request.Headers.Add(Constants.ACCEPT, Constants.APPLICATION_JSON);
                //request.Headers.Add(Constants.CONTENT_TYPE, Constants.APPLICATION_JSON);
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                //Console.WriteLine($"Token = '{authToken}'");
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    List<ProductSkusResponse> productSkusResponses = JsonConvert.DeserializeObject<List<ProductSkusResponse>>(responseContent);
                    foreach (ProductSkusResponse productSkusResponse in productSkusResponses)
                    {
                        string skuId = productSkusResponse.Id;
                        skuIds.Add(skuId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSkusFromProductId Error: {ex.Message}");
                _context.Vtex.Logger.Error("GetSkusFromProductId", null, $"Error getting skus for product id '{productId}'", ex);
            }

            return skuIds;
        }

        public async Task<bool> ProcessImageFile(string fileName, string webLink)
        {
            bool success = false;
            string identificatorType = string.Empty;
            string id = string.Empty;
            string imageName = string.Empty;
            string imageLabel = string.Empty;
            bool isMain = false;

            // IdentificatorType, Id, ImageName, ImageLabel, Main
            string[] fileNameArr = fileName.Split('.');
            if(fileNameArr.Count() == 2 && !string.IsNullOrEmpty(fileNameArr[0]))
            {
                string[] fileNameParsed = fileNameArr[0].Split(',');
                if ((fileNameParsed.Count() == 5 || fileNameParsed.Count() == 4))
                {
                    identificatorType = fileNameParsed[0];
                    id = fileNameParsed[1];
                    imageName = fileNameParsed[2];
                    imageLabel = fileNameParsed[3];
                    if (fileNameParsed.Count() == 5)
                    {
                        isMain = fileNameParsed[4].Equals("Main", StringComparison.CurrentCultureIgnoreCase);
                    }

                    Console.WriteLine($"ProcessImageFile {identificatorType} {id} Main?{isMain}");
                    _context.Vtex.Logger.Info("ProcessImageFile", null, $"{identificatorType} {id} Main?{isMain}");

                    switch(identificatorType)
                    {
                        case DriveImportConstants.IdentificatorType.SKU_ID:
                            success = await this.UpdateSkuImage(id, imageName, imageLabel, isMain, webLink);
                            _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {id} success? {success}");
                            break;
                        case DriveImportConstants.IdentificatorType.SKU_REF_ID:
                            string skuId = await this.GetSkuIdFromReference(id);
                            success = await this.UpdateSkuImage(skuId, imageName, imageLabel, isMain, webLink);
                            _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {skuId} from {identificatorType} {id} success? {success}");
                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_REF_ID:
                            string prodId = await this.GetProductIdFromReference(id);
                            List<string> prodRefSkuIds = await this.GetSkusFromProductId(prodId);
                            success = true;
                            foreach(string prodRefSku in prodRefSkuIds)
                            {
                                success &= await this.UpdateSkuImage(prodRefSku, imageName, imageLabel, isMain, webLink);
                                _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {prodRefSku} from {identificatorType} {id} success? {success}");
                            }

                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_ID:
                            List<string> skuIds = await this.GetSkusFromProductId(id);
                            success = true;
                            foreach (string sku in skuIds)
                            {
                                success &= await this.UpdateSkuImage(sku, imageName, imageLabel, isMain, webLink);
                                _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {sku} from {identificatorType} {id} success? {success}");
                            }

                            break;
                    }
                }
            }

            return success;
        }
    }
}
