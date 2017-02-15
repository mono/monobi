using System;
using System.Net;
using System.Net.Http;

namespace JenkinsRunner
{
    class Program
    {
        static void Main()
        {
            string buildUrl = Environment.GetEnvironmentVariable("UPSTREAM_BUILD_URL");
            if (string.IsNullOrWhiteSpace(buildUrl))
            {
                Console.Error.WriteLine("missing environment variable \"UPSTREAM_BUILD_URL\"");
                Environment.Exit(1);
            }

            string functionCode = Environment.GetEnvironmentVariable("FUNCTION_CODE");
            if (string.IsNullOrWhiteSpace(buildUrl))
            {
                Console.Error.WriteLine("missing environment variable \"FUNCTION_CODE\"");
                Environment.Exit(2);
            }

            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = httpClient.PostAsync($"https://monobi.azurewebsites.net/api/Post?code={functionCode}", new StringContent(buildUrl)).Result;

				Console.WriteLine($"Status Code: {(int)response.StatusCode} {response.StatusCode}");
				foreach (var header in response.Headers)
					Console.WriteLine($"{header.Key}: {string.Join(",", header.Value)}");
				Console.WriteLine();

                Console.WriteLine(response.Content.ReadAsStringAsync().Result);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    Environment.Exit(3);
                }
            }
        }
    }
}
