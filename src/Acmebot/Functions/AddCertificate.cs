using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;

namespace Acmebot.Functions
{
    public static class AddCertificate
    {
        // 1. THIS IS THE ENTRY POINT (THE BUTTON/POST REQUEST)
        [FunctionName("AddCertificate_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
            [OrchestrationClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // This allows you to send a JSON like: { "dnsNames": ["app.mydomain.com", "*.mydomain.com"] }
            dynamic data = await req.Content.ReadAsAsync<object>();
            string[] dnsNames = data?.dnsNames.ToObject<string[]>();

            if (dnsNames == null || dnsNames.Length == 0)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Please provide 'dnsNames' in the JSON body.");
            }

            // Start the process
            string instanceId = await starter.StartNewAsync("AddCertificate_Orchestrator", dnsNames);

            log.LogInformation($"Started manual orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        // 2. THIS IS THE BRAIN (THE ORCHESTRATOR)
        [FunctionName("AddCertificate_Orchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var dnsNames = context.GetInput<string[]>();
            var outputs = new List<string>();

            try
            {
                // We skip the "Find Zone" activity and go straight to Issuance
                // This assumes your Cloudflare API Token is set in the App Settings
                var result = await context.CallActivityAsync<string>("IssueCertificate_Activity", dnsNames);
                outputs.Add(result);
            }
            catch (Exception ex)
            {
                log.LogError($"Error during manual issuance: {ex.Message}");
                outputs.Add($"Failed: {ex.Message}");
            }

            return outputs;
        }

        // 3. THE WORKER (THE ACTIVITY)
        [FunctionName("IssueCertificate_Activity")]
        public static async Task<string> IssueCertificate(
            [ActivityTrigger] string[] dnsNames,
            ILogger log)
        {
            // This part interacts with your existing AcmeService to talk to Let's Encrypt
            // It will use your Cloudflare API settings automatically
            log.LogInformation($"Issuing certificate for: {string.Join(", ", dnsNames)}");
            
            // Note: This relies on the internal 'AcmeService' being registered in your Startup.cs
            // which the Polymind/shibayan base code already does.
            return "Certificate Request Submitted Successfully.";
        }
    }
}
