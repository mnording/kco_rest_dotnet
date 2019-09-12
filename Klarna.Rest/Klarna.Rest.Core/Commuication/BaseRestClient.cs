﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Klarna.Rest.Core.Commuication.Dto;
using Klarna.Rest.Core.Model;
using Klarna.Rest.Core.Serialization;
using Newtonsoft.Json;

namespace Klarna.Rest.Core.Commuication
{
    /// <summary>
    /// A base class for HTTP clients used to communicate with the Klarna APIs
    /// </summary>
    public abstract class BaseRestClient
    {
        /// <summary>
        /// Session information related to this RestClient
        /// </summary>
        protected readonly ApiSession ApiSession;

        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="apiSession">The API session instance used to communicate with Klarna APIs</param>
        /// <param name="jsonSerializer">The JSON Serializer instance to use when sending / receiving data</param>
        protected BaseRestClient(ApiSession apiSession, IJsonSerializer jsonSerializer)
        {
            ApiSession = apiSession;
            _jsonSerializer = jsonSerializer;
        }

        /// <summary>
        /// Handles HTTP POST calls
        /// </summary>
        /// <param name="url">The URL to call</param>
        /// <param name="data">The POST data to send</param>
        /// <param name="headers">The HTTP headers to send when performing a POST request</param>
        /// <returns></returns>
        protected Task Post(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            return MakeRequest(HttpMethod.Post, url, data, headers, outResponse);
        }
        
        protected async Task<T> Post<T>(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            var result = await MakeRequest(HttpMethod.Post, url, data, headers, outResponse).ConfigureAwait(false);
            return await DeserializeOrDefault<T>(result).ConfigureAwait(false);
        }

        protected Task Patch(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            return MakeRequest(new HttpMethod("PATCH"), url, data, headers, outResponse);
        }

        protected Task Delete(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            return MakeRequest(HttpMethod.Delete, url, data, headers, outResponse);
        }
        
        protected async Task<T> Delete<T>(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            var result = await MakeRequest(HttpMethod.Delete, url, data, headers, outResponse).ConfigureAwait(false);
            return await DeserializeOrDefault<T>(result).ConfigureAwait(false);
        }

        protected async Task<T> Put<T>(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            
            var result = await MakeRequest(HttpMethod.Put, url, data, headers, outResponse).ConfigureAwait(false);
            return await DeserializeOrDefault<T>(result).ConfigureAwait(false);
        }
        
        protected Task Put(string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            return MakeRequest(HttpMethod.Put, url, data, headers, outResponse);
        }

        protected async Task<T> Get<T>(
            string url, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            var result = await MakeRequest(HttpMethod.Get, url, null, headers, outResponse).ConfigureAwait(false);
            return await DeserializeOrDefault<T>(result).ConfigureAwait(false);
        }

        protected Task Get(
            string url, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> response = null)
        {
            return MakeRequest(HttpMethod.Get, url, null, headers, response);
        }

        protected async Task<Stream> GetStream(string url)
        {
            using (var client = GetClient())
            {
                var result = await client.SendAsync(GetMessage(HttpMethod.Get, url)).ConfigureAwait(false);

                await ThrowIfError(result).ConfigureAwait(false);

                return await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }
        }
        
        private async Task<HttpResponseMessage> MakeRequest(
            HttpMethod method, string url, object data = null, IDictionary<string, string> headers = null, Ref<HttpResponseMessage> outResponse = null)
        {
            var message = GetMessage(method, url, headers);
            HttpResponseMessage result;
            
            using (message.Content = GetMessageContent(data))
            {
                using (var client = GetClient())
                {
//                     Console.WriteLine("DEBUG MODE: Request\n"
//                              + ">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>\n"
//                              + method + ": " + url + "\n"
//                              + "Headers: " + headers + "\n"
//                              + "Payout: " + json + "\n");
                    
                    result = await client.SendAsync(message);
                    
//                    Console.WriteLine("DEBUG MODE: Response\n"
//                                      + "<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<\n"
//                                      + "Code: " + result.StatusCode + "\n"
//                                      + "Headers: " + Serialize(result.Headers) + "\n"
//                                      + "Body: " + await result.Content.ReadAsStringAsync() + "\n");

                    await ThrowIfError(result);
                }
            }

            if (outResponse != null)
            {
                outResponse.Value = result;
            }
            return result;
        }

        private static HttpRequestMessage GetMessage(HttpMethod method, string resource, IDictionary<string, string> headers = null)
        {
            var message = new HttpRequestMessage(method, resource);
            message.Headers.Accept.Clear();
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    message.Headers.Add(kvp.Key, kvp.Value);
                }
            }
            return message;
        }

        private HttpClient GetClient(WebProxy proxy)
        {

            var handler = new HttpClientHandler();
            if (proxy != null)
            {
                handler.Proxy = proxy;
                handler.UseProxy = true; 
            }
            if (handler.SupportsAutomaticDecompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }

            handler.UseCookies = true;
            handler.Credentials = new NetworkCredential(ApiSession.Credentials.Username, ApiSession.Credentials.Password);

            var client = new HttpClient(handler, true) {Timeout = TimeSpan.FromSeconds(600)};
            client.DefaultRequestHeaders.Add("User-Agent", ApiSession.UserAgent);
            client.DefaultRequestHeaders.ExpectContinue = false;
            return client;
        }

        private HttpContent GetMessageContent(object data)
        {
            if (data == null) return null;

            return new StringContent(Serialize(data), Encoding.UTF8, "application/json");
        }

        private async Task<T> DeserializeOrDefault<T>(HttpResponseMessage result)
        {
            var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            return !string.IsNullOrEmpty(content) ? _jsonSerializer.Deserialize<T>(content) : default(T);
        }

        private string Serialize(object data)
        {
            return _jsonSerializer.Serialize(data);
        }

        private static async Task ThrowIfError(HttpResponseMessage result)
        {
            if (!result.IsSuccessStatusCode)
            {
                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var errorMessage = new ErrorMessage();

                try
                {
                    errorMessage = JsonConvert.DeserializeObject<ErrorMessage>(content);
                }
                catch (Exception ex)
                {
                    errorMessage.ErrorMessages = new []{content};
                }

                throw new ApiException(
                    $"Error when calling {result.RequestMessage.Method.ToString().ToUpperInvariant()} {result.RequestMessage.RequestUri}.",
                    result.StatusCode,
                    errorMessage,
                    null);
            }
        }
    }
}
