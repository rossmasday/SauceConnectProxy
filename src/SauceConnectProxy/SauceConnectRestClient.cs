namespace SauceConnectProxy
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Model;
    using Newtonsoft.Json;
    using static System.FormattableString;

    public sealed class SauceConnectRestClient : IDisposable
    {
        private const string RestV1 = "v1/";
        private readonly HttpClient httpClient;
        private readonly string baseRequestUri;

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectRestClient"/> class with default Sauce labs environment variable setting for username and access key.
        /// </summary>
        public SauceConnectRestClient() 
            : this(EnvironmentConfig.SauceUsername, EnvironmentConfig.SauceAccessKey)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectRestClient"/> class.
        /// </summary>
        /// <param name="username">The usename to use to connect to the Sauce Labs.</param>
        /// <param name="accessKey">The access key to use to connect to Sauce Labs.</param>
        public SauceConnectRestClient(string username, string accessKey) 
            : this(null, username, accessKey)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SauceConnectRestClient"/> class.
        /// </summary>
        /// <param name="httpClient">In instance of <see cref="HttpClient"/>.</param>
        /// <param name="username">The usename to use to connect to the Sauce Labs.</param>
        /// <param name="accessKey">The access key to use to connect to Sauce Labs.</param>
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

        /// <summary>
        /// Retrieves all running tunnels for a specific user
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IList<string>> GetTunnelIdsAsync(CancellationToken cancellationToken)
        {
            return await GetAsync<IList<string>>(baseRequestUri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get the number of jobs that are running through the tunnel over the past 60 seconds.
        /// </summary>
        /// <param name="tunnelId">The Id of the tunnel.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        /// <returns></returns>
        public async Task<int> GetTunnelRunningJobsCountAsync(string tunnelId, CancellationToken cancellationToken)
        {
            return await GetAsync<int>(Invariant($"{baseRequestUri}/{tunnelId}/num_jobs"), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get information for a tunnel given its ID.
        /// </summary>
        /// <param name="tunnelId">The Id of the tunnel.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        /// <returns></returns>
        public async Task<TunnelInformation> GetTunnelnformationAsync(string tunnelId, CancellationToken cancellationToken)
        {
            return await GetAsync<TunnelInformation>(Invariant($"{baseRequestUri}/{tunnelId}"), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Shuts down a tunnel given its ID.
        /// </summary>
        /// <param name="tunnelId">The Id of the tunnel.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        /// <returns></returns>
        public async Task<TunnelDeleted> DeleteTunnelAsync(string tunnelId, CancellationToken cancellationToken)
        {
            var response = await this.httpClient.DeleteAsync(Invariant($"{baseRequestUri}/{tunnelId}"), cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<TunnelDeleted>(await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync());
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            httpClient?.Dispose();
        }

        private static AuthenticationHeaderValue GetAuthorisationHeader(string username, string accesskey)
        {
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Invariant($"{username}:{accesskey}"))));
        }

        private async Task<T> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
        {
            var response = await this.httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(await response.EnsureSuccessStatusCode().Content.ReadAsStringAsync().ConfigureAwait(false));
        }
    }
}
