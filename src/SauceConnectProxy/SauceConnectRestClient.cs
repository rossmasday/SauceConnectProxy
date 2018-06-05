using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SauceConnectProxy.Model;
using static System.FormattableString;

namespace SauceConnectProxy
{
    public sealed class SauceConnectRestClient : IDisposable
    {
        private const string RestV1 = "v1/";
        private readonly HttpClient httpClient;
        private readonly string baseRequestUri;

        public SauceConnectRestClient() 
            : this(Environment.GetEnvironmentVariable("SAUCE_USERNAME"), Environment.GetEnvironmentVariable("SAUCE_ACCESS_KEY"))
        {
        }

        public SauceConnectRestClient(string username, string accessKey) 
            : this(null, username, accessKey)
        {
        }

        public SauceConnectRestClient(HttpClient httpClient, string username, string accessKey)
        {
            if (string.IsNullOrEmpty(username))
                throw new ArgumentException("Value cannot be null or empty.", nameof(username));
            if (string.IsNullOrWhiteSpace(accessKey))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(accessKey));

            this.httpClient = httpClient ?? new HttpClient
            {
                BaseAddress = new Uri("https://saucelabs.com/rest/"),
                DefaultRequestHeaders = { Authorization = GetAuthorisationHeader(username, accessKey) }
            };

            this.baseRequestUri = $"{RestV1}{username}/tunnels";
        }

        public async Task<IList<string>> GetTunnelIdsAsync()
        {
            return await GetAsync<IList<string>>(baseRequestUri);
        }

        public async Task<int> GetTunnelRunningJobsCountAsync(string tunnelId)
        {
            return await GetAsync<int>(Invariant($"{baseRequestUri}/{tunnelId}/num_jobs"));
        }

        public async Task<TunnelInformation> GetTunnelnformation(string tunnelid)
        {
            return await GetAsync<TunnelInformation>(Invariant($"{baseRequestUri}/{tunnelid}"));
        }

        public async Task<TunnelDeleted> DeleteTunnel(string tunnelId)
        {
            var response = await this.httpClient.DeleteAsync(Invariant($"{baseRequestUri}/{tunnelId}"));
            return JsonConvert.DeserializeObject<TunnelDeleted>(await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync());
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }

        private static AuthenticationHeaderValue GetAuthorisationHeader(string username, string accesskey)
        {
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Invariant($"{username}:{accesskey}"))));
        }

        private async Task<T> GetAsync<T>(string requestUri)
        {
            var response = await this.httpClient.GetAsync(requestUri);
            return JsonConvert.DeserializeObject<T>(await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync());
        }
    }
}
