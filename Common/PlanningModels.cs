namespace Cleaning.DataModel;

public enum Weekday
{
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday
}

public enum TimeWindow
{
    Morning,
    Noon,
    Afternoon
}

public sealed record GeoPoint(double Latitude, double Longitude, string? H3Cell);

public sealed record RawClientCsvRow(
    string ClientId,
    string Address,
    int Bed,
    int Bath,
    int Sqft,
    bool Windows,
    bool Pets,
    bool Stairs);

public sealed record EnrichedClientCsvRow(
    string ClientId,
    string Address,
    int Bed,
    int Bath,
    int Sqft,
    bool Windows,
    bool Pets,
    bool Stairs,
    double? Latitude,
    double? Longitude,
    string? H3Cell,
    string GeocodeStatus);

public sealed record ServiceSlot(
    Weekday Day,
    TimeWindow Window,
    int DurationMinutes);

public sealed record ClientProfile(
    string ClientId,
    string Address,
    int Bed,
    int Bath,
    int Sqft,
    GeoPoint Location);

public sealed record ClientPreferences(
    bool HasPets,
    bool HasStairs,
    bool RequiresWindowCleaning,
    bool PrefersSameTeam);

public sealed record ClientSchedule(
    IReadOnlyList<ServiceSlot> RequestedSlots);

public sealed record ClientPlan(
    ClientProfile Client,
    ClientPreferences Preferences,
    ClientSchedule Schedule);

public sealed record StaffMember(
    string StaffId,
    string DisplayName);

public sealed record StaffPreferences(
    bool HasPetAllergy,
    bool LimitedMobility,
    bool CleansWindows);

public sealed record DailyStaffAvailability(
    Weekday Day,
    IReadOnlyList<TimeWindow> Windows);

public sealed record StaffSchedule(
    IReadOnlyList<DailyStaffAvailability> Availability);

public sealed record StaffPlan(
    StaffMember Member,
    StaffPreferences Preferences,
    StaffSchedule Schedule);

public sealed record CleaningTeam(
    string TeamId,
    string MemberAId,
    string MemberBId);

public sealed record PlanningScenario(
    DateOnly WeekStart,
    IReadOnlyList<ClientPlan> Clients,
    IReadOnlyList<StaffPlan> Staff,
    IReadOnlyList<CleaningTeam> Teams);

public sealed record TeamAssignment(
    string TeamId,
    string ClientId,
    Weekday Day,
    TimeWindow Window,
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
