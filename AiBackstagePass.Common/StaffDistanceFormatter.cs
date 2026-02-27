using System.Globalization;
using System.Text;

namespace AiBackstagePass.Common;

public static class StaffDistanceFormatter
{
    private const double EarthRadiusMiles = 3958.8;

    public static string BuildDistancesToOtherStaffMiles(
        IReadOnlyList<RawStaffCsvRow> staff,
        RawStaffCsvRow source,
        int decimalPlaces = 1)
    {
        if (!TryGetLocation(source, out var sourceLatitude, out var sourceLongitude))
            return string.Empty;

        var distances = new List<(string StaffId, double Miles)>(staff.Count);
        foreach (var member in staff)
        {
            if (member.StaffId.Equals(source.StaffId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!TryGetLocation(member, out var memberLatitude, out var memberLongitude))
                continue;

            var miles = MilesBetween(sourceLatitude, sourceLongitude, memberLatitude, memberLongitude);
            distances.Add((member.StaffId, miles));
        }

        if (distances.Count == 0)
            return string.Empty;

        distances.Sort((left, right) => left.Miles.CompareTo(right.Miles));

        var builder = new StringBuilder();
        for (var i = 0; i < distances.Count; i++)
        {
            if (i > 0)
                builder.Append('|');

            builder.Append(distances[i].StaffId);
            builder.Append('=');
            builder.Append(distances[i].Miles.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public static double MilesBetween(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var deltaLatitude = DegreesToRadians(latitude2 - latitude1);
        var deltaLongitude = DegreesToRadians(longitude2 - longitude1);
        var latitude1Radians = DegreesToRadians(latitude1);
        var latitude2Radians = DegreesToRadians(latitude2);

        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
            + Math.Cos(latitude1Radians) * Math.Cos(latitude2Radians) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);
        var c = 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));

        return EarthRadiusMiles * c;
    }

    private static bool TryGetLocation(RawStaffCsvRow row, out double latitude, out double longitude)
    {
        if (!row.Latitude.HasValue || !row.Longitude.HasValue)
        {
            latitude = 0;
            longitude = 0;
            return false;
        }

        latitude = row.Latitude.Value;
        longitude = row.Longitude.Value;
        return true;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
