using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace SauceConnectProxy
{
    internal sealed class GetSources
    {
        public void go()
        {
            WebRequest request = WebRequest.Create(new Uri("https://saucelabs.com/versions.json"));
            request.ContentType = "application/json";

            var response = request.GetResponseAsync().Result;
            var reader = new StreamReader(response.GetResponseStream());
            var data = new Regex("((?<=Sauce) (?=Connect)|(?<=Connect) (?=\\d))").Replace(reader.ReadToEnd(), string.Empty);

            var versionData = new
            {
                SauceConnect = new
                {
                    version = string.Empty,
                    win32 = new
                    {
                        build = string.Empty,
                        download_url = default(Uri),
                        sha1 = string.Empty
                    }
                }
            };
            var path = Path.Combine(Environment.CurrentDirectory, "sc.zip");
            var stuff = JsonConvert.DeserializeAnonymousType(data, versionData);
            using (var client = new WebClient())
            {
                client.DownloadFile(stuff.SauceConnect.win32.download_url, path);
            }

            ZipFile.ExtractToDirectory(path, Path.Combine(Environment.CurrentDirectory, "sc2"));
        }
    }
}
