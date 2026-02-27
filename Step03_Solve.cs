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

// Step 1: Pair staff based on their availability, location, and capabilities to create potential teams for each day of the week. For example, if two staff members have overlapping availability and complementary skills (e.g. one can handle pets while the other can clean windows), they could be paired together to maximize the number of clients they can serve.
Dictionary<(DayOfWeek, TimeWindows), List<Team>> potentialTeams = new(7 * 8);


// Step 2: Calculate how many open 1-hour slots are available each day based on staff
Dictionary<(DayOfWeek, int), Slot> slots = new(10 * 7);
foreach (var staff in scenario.Staff)
{
    foreach (var day in Enum.GetValues<DayOfWeek>())
    {
        var timeWindows = staff.Availability[day];
        foreach (var timeWindow in Enum.GetValues<TimeWindows>())
        {
            if (timeWindows.HasFlag(timeWindow))
            {
                var availableHours = TimeWindowDefaults.AvailableHoursByTimeWindow[timeWindow];
                foreach (var hour in availableHours)
                {
                    var slotKey = (day, hour);
                    if (!slots.ContainsKey(slotKey))
                        slots[slotKey] = new Slot(day, timeWindow, hour);
                   
                    slots[slotKey].Available++;
                    if (!staff.HasPetAllergy)
                        slots[slotKey].PetsAvailable++;
                    if (!staff.LimitedMobility)
                        slots[slotKey].StairsAvailable++;
                    if (staff.CleansWindows)
                        slots[slotKey].WindowsAvailable++;
                }
            }
        }
    }
}

// Step 3: Identify how many clients can be scheduled in each slot based on their preferences and requirements (e.g. pet-friendly, stairs, etc.)
Dictionary<ClientPlan, List<Slot>> clientSlots = 
scenario.Clients.ToDictionary(c => c, c => new List<Slot>());

foreach (var client in scenario.Clients)
{
    var durationHours = (int)Math.Ceiling(client.EstimatedHours ?? 1);
    foreach (var day in Enum.GetValues<DayOfWeek>())
    {
        var dayAvailability = new bool[10];
        var timeWindows = client.Schedule[day];
        foreach (var timeWindow in Enum.GetValues<TimeWindows>())
        {
            if (!timeWindows.HasFlag(timeWindow))
                continue;

            var availableHours = TimeWindowDefaults.AvailableHoursByTimeWindow[timeWindow];
            foreach (var hour in availableHours)
                dayAvailability[hour] = true;
        }

        for (var hour = 0; hour < dayAvailability.Length; hour++)
        {
            if (!dayAvailability[hour])
                continue;

            var canFit = true;
            for (var offset = 0; offset < durationHours; offset++)
            {
                var checkHour = hour + offset;
                if (checkHour >= dayAvailability.Length || !dayAvailability[checkHour])
                {
                    canFit = false;
                    break;
                }
            }

            if (!canFit)
                continue;

            var slotKey = (day, hour);
            if (slots.TryGetValue(slotKey, out Slot? slot))
            {
                if (client.HasPets && slot.PetsAvailable <= 0)
                    continue;
                if (client.HasStairs  && slot.StairsAvailable <= 0)
                    continue;
                if (client.RequiresWindowCleaning  && slot.WindowsAvailable <= 0)
                    continue;

                // For simplicity, we're just counting clients that could fit in the slot based on their preferences and requirements.
                // In a real scheduling algorithm, we would need to consider the actual scheduling of clients to staff and time slots, taking into account the duration of each job and potential overlaps.
                slot.Clients.Add(client);
                clientSlots[client].Add(slot);
            }
        }
    }
}

Console.WriteLine("Available slots:");
foreach (var slot in slots.Values.OrderBy(s => s.Day).ThenBy(s => s.Hour))
{
    Console.WriteLine($"{slot.Day} {slot.Hour + 8}:00 - Available: {slot.Available}, Pets: {slot.PetsAvailable}, Stairs: {slot.StairsAvailable}, Windows: {slot.WindowsAvailable}, Clients: {slot.Clients.Count}");
}

Console.WriteLine("\nClient slot matches:");
foreach (var kvp in clientSlots.OrderBy(kvp => kvp.Value.Count))
{
    var client = kvp.Key;
    var matchedSlots = kvp.Value;
    Console.Write($"{client.ClientName} ({client.ClientId}) - Matched Slots: {matchedSlots.Count}: ");
    foreach (var slot in matchedSlots.Take(5)) // Show up to 5 matched slots for brevity
        Console.Write($"{slot.Day} {slot.Hour + 8}:00 ");
    Console.WriteLine();
}

// Step 4: Weight the slots based on staff availability and client demand to identify potential bottlenecks and opportunities for scheduling optimization. For example, if a slot has high client demand but low staff availability, that could indicate a need to prioritize scheduling clients in that slot or to find ways to increase staff availability during that time.



record Team(string TeamId, StaffPlan MemberA, StaffPlan MemberB, bool CanHandlePets, bool CanHandleStairs, bool CanCleanWindows);

// Hour = 0 means 8 AM, 1 means 9 AM
record Slot(DayOfWeek Day, TimeWindows TimeWindow, int Hour)
{
    public int Available {get; set;}
    public int PetsAvailable {get; set;}
    public int StairsAvailable {get; set;}
    public int WindowsAvailable {get; set;}
    public List<Team> Teams {get;} = [];
    public List<ClientPlan> Clients {get;} = [];
}
