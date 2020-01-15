﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mmm.Platform.IoT.Common.Services.Exceptions;
using Mmm.Platform.IoT.Common.Services.Http;
using Newtonsoft.Json;

namespace Mmm.Platform.IoT.Config.Services.Helpers
{
    public class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly ILogger _logger;
        private readonly IHttpClient client;
        private Dictionary<string, string> headers;

        public HttpClientWrapper(
            ILogger<HttpClientWrapper> logger,
            IHttpClient client,
            Dictionary<string, string> headers = null)
        {
            _logger = logger;
            this.client = client;
            this.headers = headers;
            if (this.headers == null)
            {
                this.headers = new Dictionary<string, string>();
            }
        }

        public async Task<T> GetAsync<T>(
            string uri,
            string description,
            bool acceptNotFound = false)
        {
            var request = new HttpRequest();
            request.SetUriFromString(uri);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("User-Agent", "Config");
            this.AddDefaultHeaders(request);

            if (uri.ToLowerInvariant().StartsWith("https:"))
            {
                request.Options.AllowInsecureSSLServer = true;
            }

            IHttpResponse response;

            try
            {
                response = await this.client.GetAsync(request);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Request to URI {uri} failed", uri);
                throw new ExternalDependencyException($"Failed to load {description}");
            }

            if (response.StatusCode == HttpStatusCode.NotFound && acceptNotFound)
            {
                return default(T);
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("Request to URI {uri} failed with response {response}", uri, response);
                throw new ExternalDependencyException($"Unable to load {description}");
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(response.Content);
            }
            catch (Exception e)
            {
                _logger.LogError($"Could not parse result from {uri}: {e.Message}");
                throw new ExternalDependencyException($"Could not parse result from {uri}");
            }
        }

        public async Task PostAsync(
            string uri,
            string description,
            object content = null)
        {
            var request = new HttpRequest();
            request.SetUriFromString(uri);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("User-Agent", "Config");
            this.AddDefaultHeaders(request);
            if (uri.ToLowerInvariant().StartsWith("https:"))
            {
                request.Options.AllowInsecureSSLServer = true;
            }

            if (content != null)
            {
                request.SetContent(content);
            }

            IHttpResponse response;

            try
            {
                response = await this.client.PostAsync(request);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Request to URI {uri} failed", uri);
                throw new ExternalDependencyException($"Failed to post {description}");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("Request to URI {uri} failed with response {response}", uri, response);
                throw new ExternalDependencyException($"Unable to post {description}");
            }
        }

        public async Task PutAsync(
            string uri,
            string description,
            object content = null)
        {
            var request = new HttpRequest();
            request.SetUriFromString(uri);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("User-Agent", "Config");
            this.AddDefaultHeaders(request);

            if (uri.ToLowerInvariant().StartsWith("https:"))
            {
                request.Options.AllowInsecureSSLServer = true;
            }

            if (content != null)
            {
                request.SetContent(content);
            }

            IHttpResponse response;

            try
            {
                response = await this.client.PutAsync(request);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Request to URI {uri} failed", uri);
                throw new ExternalDependencyException($"Failed to put {description}");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogError("Request to URI {uri} failed with response {response}", uri, response);
                throw new ExternalDependencyException($"Unable to put {description}");
            }
        }

        private void AddDefaultHeaders(HttpRequest request)
        {
            foreach (var key in this.headers.Keys)
            {
                request.Headers.Add(key, this.headers[key]);
            }
        }
        public void SetHeaders(Dictionary<string, string> headers)
        {
            this.headers = headers;
        }
    }
}
