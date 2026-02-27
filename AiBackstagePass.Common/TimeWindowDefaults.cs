namespace AiBackstagePass.Common;

public static class TimeWindowDefaults
{
    public static readonly IReadOnlyDictionary<TimeWindows, int[]> AvailableHoursByTimeWindow = new Dictionary<TimeWindows, int[]>
    {
        [TimeWindows.None] = [],
        [TimeWindows.Morning] = [0, 1, 2], // 8 AM - 11 AM
        [TimeWindows.Noon] = [3, 4, 5], // 11 AM - 2 PM
        [TimeWindows.Afternoon] = [6, 7, 8, 9] // 2 PM - 6 PM
    };
}
