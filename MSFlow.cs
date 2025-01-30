using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{

    private static readonly string TenantId = "";
    private static readonly string ClientId = "";
    private static readonly string ClientSecret = "";


    private static readonly string PowerAutomateApiBaseUrl = "https://api.flow.microsoft.com/providers/Microsoft.ProcessSimple/environments";

    static async Task Main()
    {
        try
        {
            string accessToken = await GetAccessTokenAsync();

            if (!string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine(" Access Token Retrieved Successfully.");
                List<FlowDetails> flows = await GetAllFlows(accessToken);

                Console.WriteLine("\n Power Automate Flows Inventory:");
                Console.WriteLine("------------------------------------------------------");
                foreach (var flow in flows)
                {
                    Console.WriteLine($" Flow Name: {flow.DisplayName}");
                    Console.WriteLine($" Flow ID: {flow.FlowId}");
                    Console.WriteLine($" Last Run Time: {flow.LastRunTime}");
                    Console.WriteLine("------------------------------------------------------");
                }
            }
            else
            {
                Console.WriteLine(" Failed to retrieve access token.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" Error: {ex.Message}");
        }
    }

    private static async Task<string> GetAccessTokenAsync()
    {
        using (HttpClient client = new HttpClient())
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token");

            var keyValues = new Dictionary<string, string>
            {
                { "client_id", ClientId },
                { "client_secret", ClientSecret },
                { "scope", "https://service.flow.microsoft.com/.default" }, 
                { "grant_type", "client_credentials" }
            };

            request.Content = new FormUrlEncodedContent(keyValues);

            HttpResponseMessage response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                using JsonDocument json = JsonDocument.Parse(responseContent);
                string accessToken = json.RootElement.GetProperty("access_token").GetString();

                Console.WriteLine(" Access Token Retrieved");
                return accessToken;
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($" Failed to get access token: {response.StatusCode}");
                Console.WriteLine($"Error Details: {errorContent}");
                return null;
            }
        }
    }

    private static async Task<List<FlowDetails>> GetAllFlows(string accessToken)
    {
        List<FlowDetails> flows = new List<FlowDetails>();

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Add("x-ms-client-scope", "urn:Microsoft.Flow");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Get Environments
            string environmentsUrl = $"{PowerAutomateApiBaseUrl}?api-version=2016-11-01";
            HttpResponseMessage envResponse = await client.GetAsync(environmentsUrl);

            if (!envResponse.IsSuccessStatusCode)
            {
                string errorContent = await envResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to retrieve environments: {envResponse.StatusCode}");
                Console.WriteLine($"Error Details: {errorContent}");
                return flows;
            }

            string envContent = await envResponse.Content.ReadAsStringAsync();
            using JsonDocument envJson = JsonDocument.Parse(envContent);
            var environments = envJson.RootElement.GetProperty("value");

            foreach (var env in environments.EnumerateArray())
            {
                string envName = env.GetProperty("name").GetString();
                Console.WriteLine($" Processing Environment: {envName}");

                // Get Flows for each environment
                string flowsUrl = $"{PowerAutomateApiBaseUrl}/{envName}/flows?api-version=2016-11-01";
                HttpResponseMessage flowResponse = await client.GetAsync(flowsUrl);

                if (!flowResponse.IsSuccessStatusCode)
                {
                    string flowError = await flowResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($" Failed to retrieve flows for environment {envName}: {flowResponse.StatusCode}");
                    Console.WriteLine($"Error Details: {flowError}");
                    continue;
                }

                string flowContent = await flowResponse.Content.ReadAsStringAsync();
                using JsonDocument flowJson = JsonDocument.Parse(flowContent);
                var flowArray = flowJson.RootElement.GetProperty("value");

                foreach (var flow in flowArray.EnumerateArray())
                {
                    string flowId = flow.GetProperty("name").GetString();
                    string displayName = flow.GetProperty("properties").GetProperty("displayName").GetString();

                    flows.Add(new FlowDetails
                    {
                        FlowId = flowId,
                        DisplayName = displayName,
                        LastRunTime = "Unknown"
                    });
                }
            }
        }
        return flows;
    }
}


class FlowDetails
{
    public string FlowId { get; set; }
    public string DisplayName { get; set; }
    public string LastRunTime { get; set; }
}
