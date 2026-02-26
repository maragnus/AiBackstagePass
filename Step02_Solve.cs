#:project AiBackstagePass.Common/AiBackstagePass.Common.csproj
#:package Microsoft.Extensions.AI
#:package Microsoft.Extensions.AI.OpenAI
#:package Microsoft.Extensions.Hosting
#:package Microsoft.Extensions.Options
#:package Microsoft.Extensions.DependencyInjection
#:package OpenAI
#:property JsonSerializerIsReflectionEnabledByDefault=true

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AiBackstagePass.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

// https://platform.openai.com/settings/organization/api-keys
// dotnet user-secrets set "OpenAI:ApiKey" "..." --file "Step02_Solve.cs"
builder.Services.AddSingleton(sp =>
{
    var clientOptions = new OpenAIClientOptions
    {
        NetworkTimeout = TimeSpan.FromMinutes(30),
        RetryPolicy = new ClientRetryPolicy(0)
    };
    return new OpenAIClient(new ApiKeyCredential(builder.Configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured in user-secrets.")), clientOptions);
});

var app = builder.Build();

const string OutputClientsCsvPath = "data/clients.enriched.csv";
const string OutputStaffCsvPath = "data/staff.enriched.csv";
var scenario = DemoScenarioFactory.LoadScenario(OutputClientsCsvPath, OutputStaffCsvPath);

// Step 1: Create geographical groupings of clients based on 
var groupings = GeographicalGrouping.CreateGroups(scenario);
// Use H3 to bucket then merge into 4
// 1. Choose a territory H3 parent resolution.
// 2. Count clients per cell.
// 3. Build a graph of adjacent cells (kRing neighbors).
// 4. Merge neighboring cells until you have 4 regions, trying to balance total service minutes.

// Step 2: Create teams that can work together based on availability, start location, and capabilities for each day of the week


// Step 3: Use LLM to divvy up clients throughout the week based on day and time preferences

/*
Create a project for Logistics, it should have H3, OR-Tools, and NetTopologySuite.

It needs to provide utility method for dividing the clients into about 15-minute commute increments with the H3 grid.

It needs to provide optimal route using OR-Tools between clients taking into account estimated driving time and job duration.

I'm trying to create a library to help build [Step02_Solve.cs](Step02_Solve.cs) so 
*/