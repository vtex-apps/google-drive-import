using DriveImport.Data;
using DriveImport.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net;
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
        private readonly IDriveImportRepository _driveImportRepository;
        private readonly string _applicationName;

        public VtexAPIService(IIOServiceContext context, IVtexEnvironmentVariableProvider environmentVariableProvider, IHttpContextAccessor httpContextAccessor, IHttpClientFactory clientFactory, IDriveImportRepository driveImportRepository)
        {
            this._context = context ??
                            throw new ArgumentNullException(nameof(context));

            this._environmentVariableProvider = environmentVariableProvider ??
                                                throw new ArgumentNullException(nameof(environmentVariableProvider));

            this._httpContextAccessor = httpContextAccessor ??
                                        throw new ArgumentNullException(nameof(httpContextAccessor));

            this._clientFactory = clientFactory ??
                               throw new ArgumentNullException(nameof(clientFactory));

            this._driveImportRepository = driveImportRepository ??
                               throw new ArgumentNullException(nameof(driveImportRepository));

            this._applicationName =
                $"{this._environmentVariableProvider.ApplicationVendor}.{this._environmentVariableProvider.ApplicationName}";
        }

        public async Task<UpdateResponse> UpdateSkuImage(string skuId, string imageName, string imageLabel, bool isMain, string imageUrl)
        {
            //POST https://{{accountName}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/file
            //    {
            //                    "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //     "Url": "https://images2.alphacoders.com/509/509945.jpg"
            //    }

            bool success = false;
            string responseContent = string.Empty;
            string statusCode = string.Empty;

            if (string.IsNullOrEmpty(skuId) || string.IsNullOrEmpty(imageUrl))
            {
                responseContent = "Missing Parameter";
            }
            else
            {
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

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                        Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    //Console.WriteLine(request.RequestUri);


                    //request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);
                    if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                    {
                        //for (int cnt = 1; cnt < 2; cnt++)
                        //{
                        //await Task.Delay(cnt * 1000 * 10);
                        await Task.Delay(1000 * 20);
                        request = new HttpRequestMessage
                            {
                                Method = HttpMethod.Post,
                                RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file"),
                                Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                            };

                            if (authToken != null)
                            {
                                request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                                request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                                request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                            }

                            client = _clientFactory.CreateClient();
                            response = await client.SendAsync(request);
                        //_context.Vtex.Logger.Info("UpdateSkuImage", null, $"Sku {skuId} '{imageName}' retry ({cnt}) [{response.StatusCode}]");
                        _context.Vtex.Logger.Info("UpdateSkuImage", null, $"Sku {skuId} '{imageName}' retry  [{response.StatusCode}]");
                        //    if (response.IsSuccessStatusCode)
                        //    {
                        //        break;
                        //    }
                        //}
                    }

                    responseContent = await response.Content.ReadAsStringAsync();
                    success = response.IsSuccessStatusCode;
                    statusCode = response.StatusCode.ToString();
                    if(string.IsNullOrEmpty(responseContent))
                    {
                        responseContent = $"Updated:{success} {response.StatusCode}";
                    }
                    else if(responseContent.Contains(DriveImportConstants.ARCHIVE_CREATED))
                    {
                        // If the image was already added to the sku, consider it a success
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("UpdateSkuImage", null, $"Error updating sku '{skuId}' {imageName}", ex);
                    success = false;
                    responseContent = ex.Message;
                }
            }

            UpdateResponse updateResponse = new UpdateResponse
            {
                Success = success,
                Message = responseContent,
                StatusCode = statusCode
            };

            return updateResponse;
        }

        public async Task<UpdateResponse> UpdateSkuImageArchive(string skuId, string imageName, string imageLabel, bool isMain, string imageId)
        {
            //POST https://{{accountname}}.vtexcommercestable.com.br/api/catalog/pvt/stockkeepingunit/{{skuId}}/archive/{{imageId}}
            //    {
            //     "IsMain": true,
            //     "Label": null,
            //     "Name": "ImageName",
            //     "Text": null,
            //    }

            bool success = false;
            string responseContent = string.Empty;

            if (string.IsNullOrEmpty(skuId) || string.IsNullOrEmpty(imageId))
            {
                responseContent = "Missing Parameter";
            }
            else
            {
                try
                {
                    ImageUpdate imageUpdate = new ImageUpdate
                    {
                        IsMain = isMain,
                        Label = imageLabel,
                        Name = imageName,
                        Text = null
                    };

                    string jsonSerializedData = JsonConvert.SerializeObject(imageUpdate);

                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/archive/{imageId}"),
                        Content = new StringContent(jsonSerializedData, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                    };

                    //Console.WriteLine(request.RequestUri);


                    //request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                    string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                    if (authToken != null)
                    {
                        request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                        request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                    }

                    var client = _clientFactory.CreateClient();
                    var response = await client.SendAsync(request);

                    responseContent = await response.Content.ReadAsStringAsync();
                    success = response.IsSuccessStatusCode;
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        responseContent = $"Updated:{success} {response.StatusCode}";
                    }
                    else if (responseContent.Contains(DriveImportConstants.ARCHIVE_CREATED))
                    {
                        // If the image was already added to the sku, consider it a success
                        success = true;
                    }

                    _context.Vtex.Logger.Info("UpdateSkuImageArchive", null, $"Updating sku '{skuId}' '{imageName}' from archive '{imageId}' [{response.StatusCode}]");
                }
                catch (Exception ex)
                {
                    _context.Vtex.Logger.Error("UpdateSkuImageArchive", null, $"Error updating sku '{skuId}' {imageName}", ex);
                    success = false;
                    responseContent = ex.Message;
                }
            }

            UpdateResponse updateResponse = new UpdateResponse
            {
                Success = success,
                Message = responseContent
            };

            return updateResponse;
        }

        public async Task<bool> UpdateSkuImageByFormData(string skuId, string imageName, string imageLabel, bool isMain, byte[] imageStream)
        {
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
                MultipartFormDataContent form = new MultipartFormDataContent();
                if (isMain)
                {
                    form.Add(new ByteArrayContent(imageStream, 0, imageStream.Length), "main", imageName);
                }
                else
                {
                    form.Add(new ByteArrayContent(imageStream, 0, imageStream.Length), imageName, imageName);
                }

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog/pvt/stockkeepingunit/{skuId}/file?an={this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}"),
                    Content = new StringContent(string.Empty, Encoding.UTF8, DriveImportConstants.APPLICATION_JSON)
                };

                request.Headers.Add(DriveImportConstants.ACCEPT, DriveImportConstants.APPLICATION_JSON);
                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
                if (authToken != null)
                {
                    request.Headers.Add(DriveImportConstants.AUTHORIZATION_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.VTEX_ID_HEADER_NAME, authToken);
                    request.Headers.Add(DriveImportConstants.PROXY_AUTHORIZATION_HEADER_NAME, authToken);
                }

                //MerchantSettings merchantSettings = await _driveImportRepository.GetMerchantSettings();

                //request.Headers.Add(DriveImportConstants.APP_TOKEN, merchantSettings.AppToken);
                //request.Headers.Add(DriveImportConstants.APP_KEY, merchantSettings.AppKey);

                var client = _clientFactory.CreateClient();
                var response = await client.SendAsync(request);
                string responseContent = await response.Content.ReadAsStringAsync();

                success = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
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
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
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
                    skuId = JsonConvert.DeserializeObject<String>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkuIdFromReference", null, $"Could not get sku for reference id '{skuRefId}'  [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
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
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
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
                    ProductResponse productResponse = JsonConvert.DeserializeObject<ProductResponse>(responseContent);
                    if (productResponse != null)
                    {
                        productId = productResponse.Id.ToString();
                    }
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetProductIdFromReference", null, $"Could not get product id for reference '{productRefId}' [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
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
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
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
                else
                {
                    _context.Vtex.Logger.Warn("GetSkusFromProductId", null, $"Could not get skus for product id '{productId}'  [{response.StatusCode}]");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkusFromProductId", null, $"Error getting skus for product id '{productId}'", ex);
            }

            return skuIds;
        }

        public async Task<GetSkuContextResponse> GetSkuContext(string skuId)
        {
            // GET https://{accountName}.{environment}.com.br/api/catalog_system/pvt/sku/stockkeepingunitbyid/skuId

            GetSkuContextResponse getSkuContextResponse = null;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"http://{this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.VTEX_ACCOUNT_HEADER_NAME]}.{DriveImportConstants.ENVIRONMENT}.com.br/api/catalog_system/pvt/sku/stockkeepingunitbyid/{skuId}")
                };

                request.Headers.Add(DriveImportConstants.USE_HTTPS_HEADER_NAME, "true");
                string authToken = this._httpContextAccessor.HttpContext.Request.Headers[DriveImportConstants.HEADER_VTEX_CREDENTIAL];
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
                    getSkuContextResponse = JsonConvert.DeserializeObject<GetSkuContextResponse>(responseContent);
                }
                else
                {
                    _context.Vtex.Logger.Warn("GetSkuContext", null, $"Could not get sku for id '{skuId}'");
                }
            }
            catch (Exception ex)
            {
                _context.Vtex.Logger.Error("GetSkuContext", null, $"Error getting sku for id '{skuId}'", ex);
            }

            return getSkuContextResponse;
        }

        public async Task<UpdateResponse> ProcessImageFile(string fileName, string webLink)
        {
            UpdateResponse updateResponse = new UpdateResponse();
            List<string> messages = new List<string>();
            bool success = false;
            string identificatorType = string.Empty;
            string id = string.Empty;
            string imageName = string.Empty;
            string imageLabel = string.Empty;
            bool isMain = false;


            // IdentificatorType, Id, ImageName, ImageLabel, Main
            string[] fileNameArr = fileName.Split('.');
            if (fileNameArr.Count() == 2 && !string.IsNullOrEmpty(fileNameArr[0]))
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

                    string parsedFilename = $"[Type:{identificatorType} Id:{id} Name:{imageName} Label:{imageLabel} Main?{isMain}]";
                    long? imageId = null;

                    switch (identificatorType)
                    {
                        case DriveImportConstants.IdentificatorType.SKU_ID:
                            updateResponse = await this.UpdateSkuImage(id, imageName, imageLabel, isMain, webLink);
                            success = updateResponse.Success;
                            if (!updateResponse.Success)
                            {
                                messages.Add(updateResponse.Message);
                            }

                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                            break;
                        case DriveImportConstants.IdentificatorType.SKU_REF_ID:
                            string skuId = await this.GetSkuIdFromReference(id);
                            if (!string.IsNullOrEmpty(skuId))
                            {
                                updateResponse = await this.UpdateSkuImage(skuId, imageName, imageLabel, isMain, webLink);
                                success = updateResponse.Success;
                                if (!updateResponse.Success)
                                {
                                    messages.Add(updateResponse.Message);
                                }
                            }
                            else
                            {
                                success = false;
                                messages.Add($"Failed to find sku id from reference {id}");
                            }

                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {skuId} from {identificatorType} {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_REF_ID:
                            string prodId = await this.GetProductIdFromReference(id);
                            if (!string.IsNullOrEmpty(prodId))
                            {
                                List<string> prodRefSkuIds = await this.GetSkusFromProductId(prodId);
                                if (prodRefSkuIds != null && prodRefSkuIds.Count > 0)
                                {
                                    success = true;
                                    foreach (string prodRefSku in prodRefSkuIds)
                                    {
                                        //Console.WriteLine($"imageId='{imageId}' prodRefSku={prodRefSku}");
                                        if (imageId != null)
                                        {
                                            updateResponse = await this.UpdateSkuImageArchive(prodRefSku, imageName, imageLabel, isMain, imageId.ToString());
                                            if (!updateResponse.Success)
                                            {
                                                imageId = null;
                                                updateResponse = await this.UpdateSkuImage(prodRefSku, imageName, imageLabel, isMain, webLink);
                                            }
                                        }
                                        else
                                        {
                                            updateResponse = await this.UpdateSkuImage(prodRefSku, imageName, imageLabel, isMain, webLink);
                                        }

                                        success &= updateResponse.Success;
                                        if (!updateResponse.Success)
                                        {
                                            messages.Add(updateResponse.Message);
                                        }
                                        else if (imageId == null && !updateResponse.Message.Contains(DriveImportConstants.ARCHIVE_CREATED))
                                        {
                                            try
                                            {
                                                SkuUpdateResponse skuUpdateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(updateResponse.Message);
                                                imageId = await this.GetArchiveId(skuUpdateResponse, imageLabel);
                                                _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"Sku {prodRefSku} Image Id: {imageId}");
                                            }
                                            catch (Exception ex)
                                            {
                                                _context.Vtex.Logger.Error("ProcessImageFile", parsedFilename, $"Error parsing SkuUpdateResponse {updateResponse.Message} [{updateResponse.StatusCode}]", ex);
                                            }
                                        }

                                        _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {prodRefSku} from {identificatorType} {id} success? {success} '{updateResponse.Message}' [{updateResponse.StatusCode}]");
                                    }
                                }
                                else
                                {
                                    success = false;
                                    messages.Add($"Failed to find skus for prodcu id {prodId}");
                                }
                            }
                            else
                            {
                                success = false;
                                messages.Add($"Failed to find product id from reference {id}");
                            }

                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_ID:
                            List<string> skuIds = await this.GetSkusFromProductId(id);
                            success = true;
                            foreach (string sku in skuIds)
                            {
                                if (imageId != null)
                                {
                                    updateResponse = await this.UpdateSkuImageArchive(sku, imageName, imageLabel, isMain, imageId.ToString());
                                    if (!updateResponse.Success)
                                    {
                                        imageId = null;
                                        updateResponse = await this.UpdateSkuImage(sku, imageName, imageLabel, isMain, webLink);
                                    }
                                }
                                else
                                {
                                    updateResponse = await this.UpdateSkuImage(sku, imageName, imageLabel, isMain, webLink);
                                }

                                success &= updateResponse.Success;
                                if (!updateResponse.Success)
                                {
                                    messages.Add(updateResponse.Message);
                                }
                                else if (imageId == null && !updateResponse.Message.Contains(DriveImportConstants.ARCHIVE_CREATED))
                                {
                                    try
                                    {
                                        SkuUpdateResponse skuUpdateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(updateResponse.Message);
                                        imageId = await this.GetArchiveId(skuUpdateResponse, imageLabel);
                                        _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"Sku {sku} Image Id: {imageId}");
                                    }
                                    catch (Exception ex)
                                    {
                                        _context.Vtex.Logger.Error("ProcessImageFile", parsedFilename, $"Error parsing SkuUpdateResponse {updateResponse.Message}", ex);
                                    }
                                }

                                _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"UpdateSkuImage {sku} from {identificatorType} {id} success? {success} '{updateResponse.Message}'");
                            }

                            break;
                        default:
                            messages.Add($"Type {identificatorType} not recognized.");
                            _context.Vtex.Logger.Info("ProcessImageFile", parsedFilename, $"Type '{identificatorType}' is not recognized.  Filename '{fileName}'");
                            break;
                    }
                }
                else
                {
                    messages.Add("Invalid filename format");
                }
            }
            else
            {
                messages.Add("Invalid filename");
            }

            updateResponse.Success = success;
            updateResponse.Message = string.Join("-", messages);

            return updateResponse;
        }

        public async Task<bool> ProcessImageFile(string fileName, byte[] imageStream)
        {
            bool success = false;
            string identificatorType = string.Empty;
            string id = string.Empty;
            string imageName = string.Empty;
            string imageLabel = string.Empty;
            bool isMain = false;

            // IdentificatorType, Id, ImageName, ImageLabel, Main
            string[] fileNameArr = fileName.Split('.');
            if (fileNameArr.Count() == 2 && !string.IsNullOrEmpty(fileNameArr[0]))
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

                    _context.Vtex.Logger.Info("ProcessImageFile", null, $"{identificatorType} {id} Main?{isMain}");

                    switch (identificatorType)
                    {
                        case DriveImportConstants.IdentificatorType.SKU_ID:
                            success = await this.UpdateSkuImageByFormData(id, imageName, imageLabel, isMain, imageStream);
                            _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {id} success? {success}");
                            break;
                        case DriveImportConstants.IdentificatorType.SKU_REF_ID:
                            string skuId = await this.GetSkuIdFromReference(id);
                            success = await this.UpdateSkuImageByFormData(skuId, imageName, imageLabel, isMain, imageStream);
                            _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {skuId} from {identificatorType} {id} success? {success}");
                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_REF_ID:
                            string prodId = await this.GetProductIdFromReference(id);
                            List<string> prodRefSkuIds = await this.GetSkusFromProductId(prodId);
                            success = true;
                            foreach (string prodRefSku in prodRefSkuIds)
                            {
                                success &= await this.UpdateSkuImageByFormData(prodRefSku, imageName, imageLabel, isMain, imageStream);
                                _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {prodRefSku} from {identificatorType} {id} success? {success}");
                            }

                            break;
                        case DriveImportConstants.IdentificatorType.PRODUCT_ID:
                            List<string> skuIds = await this.GetSkusFromProductId(id);
                            success = true;
                            foreach (string sku in skuIds)
                            {
                                success &= await this.UpdateSkuImageByFormData(sku, imageName, imageLabel, isMain, imageStream);
                                _context.Vtex.Logger.Info("ProcessImageFile", null, $"UpdateSkuImage {sku} from {identificatorType} {id} success? {success}");
                            }

                            break;
                    }
                }
            }

            return success;
        }

        private async Task<long?> GetArchiveId(SkuUpdateResponse skuUpdateResponse, string imageName)
        {
            long? archiveId = null;
            for (int i = 0; i < 10; i++)
            {
                GetSkuContextResponse getSkuContextResponse = await this.GetSkuContext(skuUpdateResponse.SkuId.ToString());
                if (getSkuContextResponse != null)
                {
                    foreach (Image image in getSkuContextResponse.Images)
                    {
                        Console.WriteLine($"GetSkuContextResponse '{image.ImageName}'='{imageName}' [{image.FileId}]");
                        if (image.ImageName != null && image.ImageName.Equals(imageName))
                        {
                            archiveId = image.FileId;
                            break;
                        }
                    }
                }

                if(archiveId != null)
                {
                    break;
                }
                else
                {
                    await Task.Delay(500);
                }
            }

            _context.Vtex.Logger.Info("GetArchiveId", null, $"FileId: '{archiveId}' for '{imageName}' (sku:{skuUpdateResponse.SkuId} id:{skuUpdateResponse.Id})");

            return archiveId;
        }

        private async Task<SkuUpdateResponse> ParseSkuUpdateResponse(string responseContent)
        {
            SkuUpdateResponse updateResponse = null;
            try
            {
                updateResponse = JsonConvert.DeserializeObject<SkuUpdateResponse>(responseContent);
            }
            catch(Exception ex)
            {
                _context.Vtex.Logger.Error("ParseSkuUpdateResponse", null, $"Error parsing '{responseContent}'", ex);
            }

            return updateResponse;
        }
    }
}
