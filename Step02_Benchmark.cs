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
// dotnet user-secrets set "OpenAI:ApiKey" "..." --file "Step02_Benchmark.cs"
builder.Services.AddOptions<OpenAIOptions>().BindConfiguration("OpenAI");
builder.Services.AddSingleton<OpenAIClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIOptions>>().Value;
    var clientOptions = new OpenAIClientOptions
    {
        NetworkTimeout = TimeSpan.FromMinutes(10),
        RetryPolicy = new ClientRetryPolicy(0)
    };
    return new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions);
});

var app = builder.Build();

const string OutputClientsCsvPath = "data/clients.enriched.csv";
const string OutputStaffCsvPath = "data/staff.enriched.csv";
var scenario = DemoScenarioFactory.LoadScenario(OutputClientsCsvPath, OutputStaffCsvPath);

var openAi = app.Services.GetRequiredService<OpenAIClient>();

// string[] models = ["gpt-5-nano", "gpt-5-mini", "gpt-5.2", "gpt-5.2-codex"];
string[] models = ["gpt-5.2"];

var systemMessage = new ChatMessage(ChatRole.System,
    """
    You are a scheduling engine for a residential cleaning company.
    Build two-person teams from staff and assign clients to a day and time window.
    Follow all constraints from the data and use only the provided ids.
    Return only JSON that matches the requested schema. No markdown, no extra text.
    """);

foreach (var model in models)
{
    Console.WriteLine($"Benchmarking model: {model}");
    var client = openAi.GetChatClient(model).AsIChatClient();

    Serializers.CanonicalEncoding = false;
    await BenchmarkModel(client, model, scenario, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "json", Serializers.ToJson(scenario.Staff), Serializers.ToJson(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToNdJson(scenario.Clients));
    
    Serializers.CanonicalEncoding = true;
    Serializers.CanonicalSeparator = ":";
    await BenchmarkModel(client, model, scenario, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "json", Serializers.ToJson(scenario.Staff), Serializers.ToJson(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToNdJson(scenario.Clients));
    
    Serializers.CanonicalEncoding = true;
    Serializers.CanonicalSeparator = "_";
    await BenchmarkModel(client, model, scenario, "csv", Serializers.ToCsv(scenario.Staff), Serializers.ToCsv(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "json", Serializers.ToJson(scenario.Staff), Serializers.ToJson(scenario.Clients));
    await BenchmarkModel(client, model, scenario, "ndjson", Serializers.ToNdJson(scenario.Staff), Serializers.ToNdJson(scenario.Clients));

    // For now, only run the first model
    break;
}


async Task BenchmarkModel(IChatClient client, string model, PlanningScenario scenario, string format, string staff, string clients)
{
    var canonical = Serializers.CanonicalEncoding ? "Canonical" : "Verbose";
    var separator = Serializers.CanonicalSeparator == ":" ? "Colon" : "Underscore";
    Console.WriteLine($"Model: {model}, Encoding: {canonical}, Separator: {separator}, Format: {format}");

    var scheduleMessage = new ChatMessage(ChatRole.User,
        $$"""
        Input format: {{format}}
        Staff:
        {{staff}}

        Clients:
        {{clients}}

        Interpretation:
        - Use `id` as the identifier.
        - `windows` means "can clean windows" for staff and `windows` means "requires window cleaning" for clients
        - `nopets` means "has pet allergy" for staff and `pets` means "has pets" for clients.
        - `nostairs` means "limited mobility" for staff and `stairs` means "has stairs" for clients.
        - `sunday`..`saturday` are availability windows. Values may be `Morning,Noon,Afternoon` or encoded as `Mo:A`, `Tu:N`, `Fr:P`.
        - `None` or `X` means unavailable.
        - Do not consider travel time or client hours for this exercise.

        Task:
        Create two-person teams and assign each client to a team, day (Monday-Friday), and time window (Morning/Noon/Afternoon).
        Distribute assignments evenly across teams and days as much as possible.

        Hard constraints:
        - Client availability must include the assigned day+window.
        - Both staff members must be available in that day+window.
        - If a client requires window cleaning, at least one staff member must clean windows.
        - If a client has pets, no staff member may have pet allergy.
        - If a client has stairs, no staff member may have limited mobility.
        - A team can only visit two clients per time window per day.

        If a client cannot be scheduled, include its id in `unassignedClientIds` and do not assign it.
        """);

    var options = new ChatOptions
    {
        Reasoning = new ReasoningOptions() { Effort = ReasoningEffort.High, Output = ReasoningOutput.Full }
    };
    var stopwatch = Stopwatch.StartNew();
    var response = await client.GetResponseAsync<SchedulingResponse>([systemMessage, scheduleMessage], options);
    stopwatch.Stop();

    Console.WriteLine($"Latency: {stopwatch.Elapsed.TotalSeconds:0} s, Tokens: input={response.Usage?.InputTokenCount}, output={response.Usage?.OutputTokenCount}, reasoning={response.Usage?.ReasoningTokenCount}, total={response.Usage?.TotalTokenCount}, unassigned={response.Result.UnassignedClientIds.Length}");

    using var file = File.CreateText($"response.{model}.{canonical}.{separator}.{format}.txt");
    file.WriteLine($"Model: {model}");
    file.WriteLine($"Encoding: {canonical}");
    file.WriteLine($"Separator: {separator}");
    file.WriteLine($"Format: {format}");
    file.WriteLine();
    file.WriteLine(scheduleMessage.Text);
    file.WriteLine();
    file.WriteLine("---");
    file.WriteLine();
    file.WriteLine(JsonSerializer.Serialize(response.Result, Serializers.JsonOptionsIndented));

    ValidateResponse(scenario, response.Result);
}

void ValidateResponse(PlanningScenario scenario, SchedulingResponse response)
{
    var staffById = scenario.Staff.ToDictionary(staff => staff.StaffId, StringComparer.OrdinalIgnoreCase);
    var clientById = scenario.Clients.ToDictionary(client => client.ClientId, StringComparer.OrdinalIgnoreCase);
    var teamsById = response.Teams.ToDictionary(team => team.TeamId, StringComparer.OrdinalIgnoreCase);
    var assignedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var unassigned = new HashSet<string>(response.UnassignedClientIds, StringComparer.OrdinalIgnoreCase);
    var errors = new List<string>();

    foreach (var team in response.Teams)
    {
        if (!staffById.ContainsKey(team.MemberAId))
            errors.Add($"Team {team.TeamId} member missing: {team.MemberAId}");
        if (!staffById.ContainsKey(team.MemberBId))
            errors.Add($"Team {team.TeamId} member missing: {team.MemberBId}");
        if (team.MemberAId.Equals(team.MemberBId, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Team {team.TeamId} has duplicate members: {team.MemberAId}");
    }

    foreach (var assignment in response.Assignments)
    {
        if (!teamsById.TryGetValue(assignment.TeamId, out var team))
            errors.Add($"Unknown team: {assignment.TeamId}");

        if (!clientById.TryGetValue(assignment.ClientId, out var client))
            errors.Add($"Unknown client: {assignment.ClientId}");

        if (!TryParseDay(assignment.Day, out var day))
            errors.Add($"Invalid day '{assignment.Day}' for client {assignment.ClientId}");

        if (!TryParseWindow(assignment.Window, out var window))
            errors.Add($"Invalid window '{assignment.Window}' for client {assignment.ClientId}");

        if (team is null || client is null || !TryParseDay(assignment.Day, out day) || !TryParseWindow(assignment.Window, out window))
            continue;

        if (!staffById.TryGetValue(team.MemberAId, out var memberA))
            errors.Add($"Unknown staff {team.MemberAId} on team {team.TeamId}");

        if (!staffById.TryGetValue(team.MemberBId, out var memberB))
            errors.Add($"Unknown staff {team.MemberBId} on team {team.TeamId}");

        if (!IsAvailable(client.Schedule, day, window))
            errors.Add($"Client unavailable: {assignment.ClientId} on {assignment.Day} {assignment.Window}");

        if (memberA is null || memberB is null)
            continue;
        
        if (!IsAvailable(memberA.Availability, day, window))
            errors.Add($"Staff unavailable: {memberA.StaffId} on {assignment.Day} {assignment.Window}");
        if (!IsAvailable(memberB.Availability, day, window))
            errors.Add($"Staff unavailable: {memberB.StaffId} on {assignment.Day} {assignment.Window}");

        if (client.RequiresWindowCleaning && !memberA.CleansWindows && !memberB.CleansWindows)
            errors.Add($"Window cleaning required with no window staff: {assignment.ClientId}");
        if (client.HasPets && (memberA.HasPetAllergy || memberB.HasPetAllergy))
            errors.Add($"Pet allergy conflict: {assignment.ClientId}");
        if (client.HasStairs && (memberA.LimitedMobility || memberB.LimitedMobility))
            errors.Add($"Stairs with limited mobility staff: {assignment.ClientId}");

        if (assignedCounts.TryGetValue(assignment.ClientId, out var count))
            assignedCounts[assignment.ClientId] = count + 1;
        else
            assignedCounts[assignment.ClientId] = 1;
    }

    foreach (var (clientId, count) in assignedCounts)
        if (count > 1)
            errors.Add($"Client assigned multiple times: {clientId} ({count})");

    foreach (var client in scenario.Clients)
    {
        var assigned = assignedCounts.ContainsKey(client.ClientId);
        var listed = unassigned.Contains(client.ClientId);
        if (!assigned && !listed)
            errors.Add($"Client not assigned or listed unassigned: {client.ClientId}");
        if (assigned && listed)
            errors.Add($"Client assigned but listed as unassigned: {client.ClientId}");
    }

    foreach (var clientId in response.UnassignedClientIds)
        if (!clientById.ContainsKey(clientId))
            errors.Add($"Unknown unassigned client: {clientId}");

    if (errors.Count == 0)
        Console.WriteLine("Validation: OK");
    else
    {
        Console.WriteLine($"Validation: {errors.Count} error(s)");
        foreach (var error in errors)
            Console.WriteLine($"- {error}");
    }
}

static bool TryParseDay(string dayText, out DayOfWeek day)
{
    if (!Enum.TryParse(dayText, true, out day))
        return false;

    return day is DayOfWeek.Monday or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday;
}

static bool TryParseWindow(string windowText, out TimeWindows window)
{
    if (!Enum.TryParse(windowText, true, out window))
        return false;

    return window is TimeWindows.Morning or TimeWindows.Noon or TimeWindows.Afternoon;
}

static bool IsAvailable(Schedule schedule, DayOfWeek day, TimeWindows window)
{
    var windows = day switch
    {
        DayOfWeek.Sunday => schedule.Sunday,
        DayOfWeek.Monday => schedule.Monday,
        DayOfWeek.Tuesday => schedule.Tuesday,
        DayOfWeek.Wednesday => schedule.Wednesday,
        DayOfWeek.Thursday => schedule.Thursday,
        DayOfWeek.Friday => schedule.Friday,
        DayOfWeek.Saturday => schedule.Saturday,
        _ => TimeWindows.None
    };

    return windows.HasFlag(window);
}

record SchedulingResponse(CleaningTeam[] Teams, ScheduledAssignment[] Assignments, string[] UnassignedClientIds, string[] Notes);
record ScheduledAssignment(string TeamId, string ClientId, string Day, string Window, int DurationMinutes, int Sequence, string Notes);


public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
}
