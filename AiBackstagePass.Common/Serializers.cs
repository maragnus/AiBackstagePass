using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using nietras.SeparatedValues;

namespace AiBackstagePass.Common;

public static class Serializers
{
    public static bool CanonicalEncoding { get; set; }
    public static string CanonicalSeparator { get; set; } = ":";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions JsonOptionsIndented = new JsonSerializerOptions(JsonOptions) { WriteIndented = true };

    private static string Encode(DayOfWeek dayOfWeek, TimeWindows timeWindows)
    {
        var day = CanonicalEncoding ? dayOfWeek.ToString()[..2] : "";

        if (timeWindows == TimeWindows.None)
            return CanonicalEncoding ? $"{day}{CanonicalSeparator}X" : "None";

        var parts = new List<string>();
        if (timeWindows.HasFlag(TimeWindows.Morning))
            parts.Add(CanonicalEncoding ? $"{day}{CanonicalSeparator}A" : "Morning");
        if (timeWindows.HasFlag(TimeWindows.Noon))
            parts.Add(CanonicalEncoding ? $"{day}{CanonicalSeparator}N" : "Noon");
        if (timeWindows.HasFlag(TimeWindows.Afternoon))
            parts.Add(CanonicalEncoding ? $"{day}{CanonicalSeparator}P" : "Afternoon");

        return string.Join(",", parts);
    }

    private static string Encode(string prefix, bool value) => CanonicalEncoding ? $"{prefix}{CanonicalSeparator}{(value ? "Y" : "N")}" : (value ? "Yes" : "No");

    public static string ToCsv(IEnumerable<ClientPlan> clients)
    {
        using var writer = Sep.Writer().ToText();
        foreach (var client in clients)
        {
            using var row = writer.NewRow();
            row["client id"].Set(client.ClientId);
            row["h3"].Set(client.Location.H3Cell);
            row["windows"].Set(Encode("W", client.RequiresWindowCleaning));
            row["pets"].Set(Encode("P", client.HasPets));
            row["stairs"].Set(Encode("S", client.HasStairs));
            row["hours"].Format(client.EstimatedHours ?? 1.0, "0.0");
            row["sunday"].Set(Encode(DayOfWeek.Sunday, client.Schedule.Sunday));
            row["monday"].Set(Encode(DayOfWeek.Monday, client.Schedule.Monday));
            row["tuesday"].Set(Encode(DayOfWeek.Tuesday, client.Schedule.Tuesday));
            row["wednesday"].Set(Encode(DayOfWeek.Wednesday, client.Schedule.Wednesday));
            row["thursday"].Set(Encode(DayOfWeek.Thursday, client.Schedule.Thursday));
            row["friday"].Set(Encode(DayOfWeek.Friday, client.Schedule.Friday));
            row["saturday"].Set(Encode(DayOfWeek.Saturday, client.Schedule.Saturday));
        }
        return writer.ToString();
    }
    
    public static string ToCsv(IEnumerable<StaffPlan> staff)
    {
        using var writer = Sep.Writer().ToText();
        foreach (var member in staff)
        {
            using var row = writer.NewRow();
            row["staff id"].Set(member.StaffId);
            row["h3"].Set(member.Location.H3Cell);
            row["windows"].Set(Encode("W", member.CleansWindows));
            row["nopets"].Set(Encode("P", member.HasPetAllergy));
            row["nostairs"].Set(Encode("S", member.LimitedMobility));
            row["sunday"].Set(Encode(DayOfWeek.Sunday, member.Availability.Sunday));
            row["monday"].Set(Encode(DayOfWeek.Monday, member.Availability.Monday));
            row["tuesday"].Set(Encode(DayOfWeek.Tuesday, member.Availability.Tuesday));
            row["wednesday"].Set(Encode(DayOfWeek.Wednesday, member.Availability.Wednesday));
            row["thursday"].Set(Encode(DayOfWeek.Thursday, member.Availability.Thursday));
            row["friday"].Set(Encode(DayOfWeek.Friday, member.Availability.Friday));
            row["saturday"].Set(Encode(DayOfWeek.Saturday, member.Availability.Saturday));
        }
        return writer.ToString();
    }

    private static object EncodeClient(ClientPlan client)
    {
        return new
        {
            client.ClientId,
            H3 = client.Location.H3Cell,
            Windows = Encode("W", client.RequiresWindowCleaning),
            Pets = Encode("P", client.HasPets),
            Stairs = Encode("S", client.HasStairs),
            Hours = client.EstimatedHours ?? 1.0,
            Sunday = Encode(DayOfWeek.Sunday, client.Schedule.Sunday),
            Monday = Encode(DayOfWeek.Monday, client.Schedule.Monday),
            Tuesday = Encode(DayOfWeek.Tuesday, client.Schedule.Tuesday),
            Wednesday = Encode(DayOfWeek.Wednesday, client.Schedule.Wednesday),
            Thursday = Encode(DayOfWeek.Thursday, client.Schedule.Thursday),
            Friday = Encode(DayOfWeek.Friday, client.Schedule.Friday),
            Saturday = Encode(DayOfWeek.Saturday, client.Schedule.Saturday)
        };
    }

    private static object EncodeStaff(StaffPlan staff)
    {
        return new
        {
            staff.StaffId,
            H3 = staff.Location.H3Cell,
            Windows = Encode("W", staff.CleansWindows),
            NoPets = Encode("P", staff.HasPetAllergy),
            NoStairs = Encode("S", staff.LimitedMobility),
            Sunday = Encode(DayOfWeek.Sunday, staff.Availability.Sunday),
            Monday = Encode(DayOfWeek.Monday, staff.Availability.Monday),
            Tuesday = Encode(DayOfWeek.Tuesday, staff.Availability.Tuesday),
            Wednesday = Encode(DayOfWeek.Wednesday, staff.Availability.Wednesday),
            Thursday = Encode(DayOfWeek.Thursday, staff.Availability.Thursday),
            Friday = Encode(DayOfWeek.Friday, staff.Availability.Friday),
            Saturday = Encode(DayOfWeek.Saturday, staff.Availability.Saturday)
        };
    }

    public static string ToJson(IEnumerable<ClientPlan> clients, bool indented = true)
    {
        return JsonSerializer.Serialize(clients.Select(EncodeClient), indented ? JsonOptionsIndented : JsonOptions);
    }
    
    public static string ToJson(IEnumerable<StaffPlan> staff, bool indented = true)
    {
        return JsonSerializer.Serialize(staff.Select(EncodeStaff), indented ? JsonOptionsIndented : JsonOptions);
    }

    public static string ToNdJson(IEnumerable<ClientPlan> clients)
    {
        var result = new StringBuilder();
        foreach (var client in clients)
        {
            var json = JsonSerializer.Serialize(EncodeClient(client), JsonOptions);
            result.AppendLine(json);
        }
        return result.ToString();
    }
    
    public static string ToNdJson(IEnumerable<StaffPlan> staff)
    {
        var result = new StringBuilder();
        foreach (var member in staff)
        {
            var json = JsonSerializer.Serialize(EncodeStaff(member), JsonOptions);
            result.AppendLine(json);
        }
        return result.ToString();
    }

}