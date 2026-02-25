namespace AiBackstagePass.Common;

public enum Weekday
{
    Sunday,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday
}

[Flags]
public enum TimeWindows
{
    None = 0,
    Morning = 1 << 0,
    Noon = 1 << 1,
    Afternoon = 1 << 2
}

public sealed record GeoPoint(double Latitude, double Longitude, string? H3Cell);

public sealed record ClientCsvRow(
    string ClientId,
    string ClientName,
    string Address,
    int Bed,
    int Bath,
    int Sqft,
    bool Windows,
    bool Pets,
    bool Stairs,
    string Monday,
    string Tuesday,
    string Wednesday,
    string Thursday,
    string Friday,
    bool PrefersSameTeam,
    string? PreferredStaffId,
    double? Latitude,
    double? Longitude,
    string? H3Cell,
    string? GeocodeStatus);

public sealed record RawStaffCsvRow(
    string StaffId,
    string StaffName,
    string Address,
    bool HasPetAllergy,
    bool LimitedMobility,
    bool CleansWindows,
    string Monday,
    string Tuesday,
    string Wednesday,
    string Thursday,
    string Friday,
    double? Latitude,
    double? Longitude,
    string? H3Cell,
    string? GeocodeStatus);

public sealed record Schedule(TimeWindows Sunday, TimeWindows Monday, TimeWindows Tuesday, TimeWindows Wednesday, TimeWindows Thursday, TimeWindows Friday, TimeWindows Saturday)
{
    public Schedule(string sunday, string monday, string tuesday, string wednesday, string thursday, string friday, string saturday)
            : this(ToTimeWindows(sunday), ToTimeWindows(monday), ToTimeWindows(tuesday), ToTimeWindows(wednesday), ToTimeWindows(thursday), ToTimeWindows(friday), ToTimeWindows(saturday))
    {
    }

    public static TimeWindows ToTimeWindows(string str)
    {
        var windows = TimeWindows.None;
        foreach (var ch in str)
        {
            windows |= ch switch
            {
                'A' => TimeWindows.Morning,
                'N' => TimeWindows.Noon,
                'P' => TimeWindows.Afternoon,
                _ => throw new FormatException($"Invalid time window character '{ch}'"),
            };
        }
        return windows;
    }
}

public sealed record ClientPlan(
    string ClientId,
    string ClientName,
    string Address,
    int Bed,
    int Bath,
    int Sqft,
    GeoPoint Location,
    bool HasPets,
    bool HasStairs,
    bool RequiresWindowCleaning,
    bool PrefersSameTeam,
    string? PreferredStaffId,
    Schedule Schedule);

public sealed record DailyStaffAvailability(
    Weekday Day,
    TimeWindows Windows);

public sealed record StaffPlan(
    string StaffId,
    string DisplayName,
    string Address,
    GeoPoint Location,
    bool HasPetAllergy,
    bool LimitedMobility,
    bool CleansWindows,
    Schedule Availability);

public sealed record CleaningTeam(
    string TeamId,
    string MemberAId,
    string MemberBId);

public sealed record PlanningScenario(
    DateOnly WeekStart,
    IReadOnlyList<ClientPlan> Clients,
    IReadOnlyList<StaffPlan> Staff);

public sealed record TeamAssignment(
    string TeamId,
    string ClientId,
    Weekday Day,
    TimeWindows Window,
    int DurationMinutes,
    int Sequence,
    string Notes);

public sealed record OptimizationPlan(
    string Model,
    string PromptVariant,
    DateTimeOffset GeneratedAtUtc,
    PlanningScenario Scenario,
    IReadOnlyList<TeamAssignment> Assignments,
    string? RawModelResponse);

public sealed record RouteStop(
    int Sequence,
    string TeamId,
    Weekday Day,
    string ClientId,
    string Address,
    double Latitude,
    double Longitude,
    string? H3Cell,
    int DriveMinutesFromPrevious,
    int ServiceMinutes,
    TimeOnly Arrival,
    TimeOnly Departure);

public sealed record DailyTeamRoute(
    Weekday Day,
    string TeamId,
    int TotalDriveMinutes,
    int TotalServiceMinutes,
    IReadOnlyList<RouteStop> Stops);

public sealed record RoutePlan(
    DateTimeOffset GeneratedAtUtc,
    string SourceOptimizationFile,
    IReadOnlyList<DailyTeamRoute> Routes);
