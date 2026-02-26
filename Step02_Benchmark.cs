#:project AiBackstagePass.Common/AiBackstagePass.Common.csproj
#:package Microsoft.Extensions.AI
#:package Microsoft.Extensions.Hosting
#:package Microsoft.Extensions.Options
#:package Microsoft.Extensions.DependencyInjection
#:package OpenAI
#:property JsonSerializerIsReflectionEnabledByDefault=true

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
// dotnet user-secrets set "OpenAI:ApiKey" "..." --file "Step02_Benchmark.cs"
builder.Services.AddOptions<OpenAIOptions>().BindConfiguration("OpenAI");
builder.Services.AddSingleton<OpenAIClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    return new OpenAIClient(options.ApiKey);
});

var app = builder.Build();

const string OutputClientsCsvPath = "data/clients.enriched.csv";
const string OutputStaffCsvPath = "data/staff.enriched.csv";
var scenario = DemoScenarioFactory.LoadScenario(OutputClientsCsvPath, OutputStaffCsvPath);

var openAi = app.Services.GetRequiredService<OpenAIClient>();

string[] models = ["gpt-5-nano", "gpt-5-mini", "gpt-5.2", "gpt-5.2-codex"];

foreach (var model in models)
{
    Console.WriteLine($"Benchmarking model: {model}");

    Serializers.CanonicalEncoding = false;
    await BenchmarkModel(model, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "json", Serializers.ToJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    
    Serializers.CanonicalEncoding = true;
    Serializers.CanonicalSeparator = ":";
    await BenchmarkModel(model, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "json", Serializers.ToJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    
    Serializers.CanonicalEncoding = true;
    Serializers.CanonicalSeparator = "_";
    await BenchmarkModel(model, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "json", Serializers.ToJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(model, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToCsv(scenario.Clients));

    // For now, only run the first model
    break;
}

async Task BenchmarkModel(string model, string format, string staff, string clients)
{
    var client = openAi.GetChatClient(model);
    var canonical = Serializers.CanonicalEncoding ? "Canonical" : "Verbose";
    var separator = Serializers.CanonicalSeparator == ":" ? "Colon" : "Underscore";
    Console.WriteLine($"Model: {model}, Encoding: {canonical}, Separator: {separator}, Format: {format}");

    // TODO - Create teams and put clients on preferred days.

}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
}