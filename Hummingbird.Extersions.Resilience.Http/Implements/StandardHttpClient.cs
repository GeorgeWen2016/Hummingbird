﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Hummingbird.Extersions.Resilience.Http
{
    public class StandardHttpClient : IHttpClient
    {
        private HttpClient _client;
        private ILogger<StandardHttpClient> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _compomentName = typeof(StandardHttpClient).FullName;

        public StandardHttpClient(ILogger<StandardHttpClient> logger, IHttpContextAccessor httpContextAccessor)
        {
            _client = new HttpClient();
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task<HttpResponseMessage> PostAsync<T>(string uri, T item, string authorizationToken = null,  string authorizationMethod = "Bearer", IDictionary<string, string> dictionary = null)
        {
          
                return await DoPostPutAsync(HttpMethod.Post, uri, item, authorizationToken, authorizationMethod, dictionary);
            
        }

        public async Task<HttpResponseMessage> PutAsync<T>(string uri, T item, string authorizationToken = null, string authorizationMethod = "Bearer", IDictionary<string, string> dictionary = null)
        {
                return await DoPostPutAsync(HttpMethod.Put, uri, item, authorizationToken, authorizationMethod, dictionary);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string uri, string authorizationToken = null, string authorizationMethod = "Bearer", IDictionary<string, string> dictionary = null)
        {
            using (var tracer = new Hummingbird.Extensions.Tracing.Tracer("HTTP DELETE"))
            {
                tracer.SetComponent(_compomentName);
                tracer.SetTag("http.url", uri);
                tracer.SetTag("http.method", "DELETE");

                var requestMessage = new HttpRequestMessage(HttpMethod.Delete, uri);

                SetAuthorizationHeader(requestMessage);

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                if (dictionary != null)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        requestMessage.Headers.Add(key, dictionary[key]);
                    }
                }

                var response = await _client.SendAsync(requestMessage);
                var responseContent = await response.Content.ReadAsStringAsync();

                #region LOG:记录返回
                tracer.SetTag("http.status_code", (int)response.StatusCode);

                if (dictionary != null && dictionary.ContainsKey("x-masking") && (dictionary["x-masking"] == "all" || dictionary["x-masking"] == "response"))
                {
                    //日志脱敏不记录
                }
                else
                {
                    tracer.LogResponse(responseContent);
                }
                #endregion

                return response;
            }
        }

        public async Task<string> GetStringAsync(string uri, string authorizationToken = null, string authorizationMethod = "Bearer", IDictionary<string, string> dictionary = null)
        {
            using (var tracer = new Hummingbird.Extensions.Tracing.Tracer("HTTP GET"))
            {
                tracer.SetComponent(_compomentName);
                tracer.SetTag("http.url", uri);
                tracer.SetTag("http.method", "GET");
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri);

                SetAuthorizationHeader(requestMessage);

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                if (dictionary != null)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        requestMessage.Headers.Add(key, dictionary[key]);
                    }
                }

                var response = await _client.SendAsync(requestMessage);
                var responseContent = await response.Content.ReadAsStringAsync();

                #region LOG:记录返回
                tracer.SetTag("http.status_code", (int)response.StatusCode);

                if (dictionary != null && dictionary.ContainsKey("x-masking") && (dictionary["x-masking"] == "all" || dictionary["x-masking"] == "response"))
                {
                    //日志脱敏不记录
                }
                else
                {
                    tracer.LogResponse(responseContent);
                }
                #endregion

                return responseContent;
            }
        }


        private void SetAuthorizationHeader(HttpRequestMessage requestMessage)
        {
            var authorizationHeader = _httpContextAccessor.HttpContext.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                requestMessage.Headers.Add("Authorization", new List<string>() { authorizationHeader });
            }
        }

        private async Task<HttpResponseMessage> DoPostPutAsync<T>(HttpMethod method, string uri, T item, string authorizationToken = null, string authorizationMethod = "Bearer", IDictionary<string, string> dictionary = null)
        {
            using (var tracer = new Hummingbird.Extensions.Tracing.Tracer($"HTTP {method.Method.ToUpper()}"))
            {
                tracer.SetComponent(_compomentName);
                tracer.SetTag("http.url", uri);
                tracer.SetTag("http.method", method.Method.ToUpper());
                if (method != HttpMethod.Post && method != HttpMethod.Put)
                {
                    throw new ArgumentException("Value must be either post or put.", nameof(method));
                }

                var requestMessage = new HttpRequestMessage(method, uri);
                var requestContent = JsonConvert.SerializeObject(item);

                #region LOG:记录请求
                if (dictionary != null && dictionary.ContainsKey("x-masking") && (dictionary["x-masking"] == "all" || dictionary["x-masking"] == "request"))
                {
                    //日志脱敏不记录
                }
                else
                {
                    tracer.LogRequest(requestContent);
                }
                #endregion

                SetAuthorizationHeader(requestMessage);

                requestMessage.Content = new StringContent(requestContent, System.Text.Encoding.UTF8, "application/json");

                if (authorizationToken != null)
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue(authorizationMethod, authorizationToken);
                }

                if (dictionary != null)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        requestMessage.Headers.Add(key, dictionary[key]);
                    }
                }



                var response = await _client.SendAsync(requestMessage);

                #region LOG:记录返回结果和响应
                var responseContent = await response.Content.ReadAsStringAsync();
                tracer.SetTag("http.status_code", (int)response.StatusCode);
                if (dictionary != null && dictionary.ContainsKey("x-masking") && (dictionary["x-masking"] == "all" || dictionary["x-masking"] == "response"))
                {
                    //日志脱敏不记录
                }
                else
                {
                    tracer.LogResponse(responseContent);
                }
                #endregion

                if (response.StatusCode == HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException();
                }

                return response;
            }
        }

    }
}

