using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using MMM.Azure.IoTSolutions.TenantManager.Services.Http;
using HttpRequest = MMM.Azure.IoTSolutions.TenantManager.Services.Http.HttpRequest;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MMM.Azure.IoTSolutions.TenantManager.Services.Helpers
{
    public interface IExternalRequestHelper
    {
        Task<T> ProcessApiModelRequestAsync<T>(HttpMethod method, string url, string tenantId = null);
        Task<T> ProcessApiModelRequestAsync<T>(HttpMethod method, string url, T content, string tenantId = null);
    }

    public class ExternalRequestHelper : IExternalRequestHelper
    {
        private const string TENANT_HEADER = "ApplicationTenantID";
        private const string AZDS_ROUTE_KEY = "azds-route-as";

        private IHttpClient _httpClient;
        private IHttpContextAccessor _httpContextAccessor;

        public ExternalRequestHelper(IHttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            this._httpClient = httpClient;
            this._httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Process an External Dependency Request using the given parameters to create a generic HttpRequest and deserialize the response to type T
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="tenantId"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<T> ProcessApiModelRequestAsync<T>(HttpMethod method, string url, string tenantId = null)
        {
            HttpRequest request = this.CreateRequest(url, tenantId);
            return await this.SendRequestAsync<T>(method, request);
        }

        /// <summary>
        /// Process an External Dependency Request using the given parameters to create a generic HttpRequest and deserialize the body and response to type T 
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="content"></param>
        /// <param name="tenantId"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<T> ProcessApiModelRequestAsync<T>(HttpMethod method, string url, T content, string tenantId = null)
        {
            HttpRequest request = this.CreateRequest(url, content, tenantId);
            return await this.SendRequestAsync<T>(method, request);
        }

        /// <summary>
        /// Send an HttpRequest using the given HTTP method, deserialize the response to type T
        /// </summary>
        /// <param name="method"></param>
        /// <param name="request"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private async Task<T> SendRequestAsync<T>(HttpMethod method, HttpRequest request)
        {
            IHttpResponse response = null;
            try
            {
                response = await this._httpClient.SendAsync(request, method);
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception("Http Request returned a status code other than 200.");
                }
            }
            catch (Exception e)
            {
                throw new Exception("An error occurred while sending the request.", e);
            }

            string responseContent = response?.Content?.ToString();
            if (String.IsNullOrEmpty(responseContent))
            {
                throw new Exception("The response was null or did not contain any content. Unable deserialize to the proper API model.");
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(responseContent);
            }
            catch (Exception e)
            {
                throw new JsonReaderException("Unable to deserialize response content to the proper API model.", e);
            }
        }

        /// <summary>
        /// Create an HttpRequest with the necessary parameters for an External Dependency API request
        /// </summary>
        /// <param name="url"></param>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        private HttpRequest CreateRequest(string url, string tenantId = null)
        {
            var request = new HttpRequest();
            request.SetUriFromString(url);

            request.AddHeader(TENANT_HEADER, tenantId);

            if (url.ToLowerInvariant().StartsWith("https:"))
            {
                request.Options.AllowInsecureSSLServer = true;
            }

            if (this._httpContextAccessor.HttpContext.Request.Headers.ContainsKey(AZDS_ROUTE_KEY))
            {
                try
                {
                    var azdsRouteAs = this._httpContextAccessor.HttpContext.Request.Headers.First(p => String.Equals(p.Key, AZDS_ROUTE_KEY, StringComparison.OrdinalIgnoreCase));
                    request.Headers.Add(AZDS_ROUTE_KEY, azdsRouteAs.Value.First());  // azdsRouteAs.Value returns an iterable of strings, take the first
                }
                catch (Exception e)
                {
                    throw new Exception($"Unable to attach the {AZDS_ROUTE_KEY} header to the IdentityGatewayClient Request.", e);
                }
            }

            return request;
        }

        /// <summary>
        /// Create an HttpRequest with the necessary parameters for an External Dependency API request
        /// </summary>
        /// <param name="url"></param>
        /// <param name="content"></param>
        /// <param name="tenantId"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private HttpRequest CreateRequest<T>(string url, T content, string tenantId)
        {
            HttpRequest request = this.CreateRequest(url, tenantId);
            request.SetContent(content);
            return request;
        }
    }
}