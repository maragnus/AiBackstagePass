using System.Globalization;
using nietras.SeparatedValues;

namespace AiBackstagePass.Common;

public static class DemoScenarioFactory
{
    private static readonly string[] BaseClientColumns =
    [
        "ClientId",
        "ClientName",
        "Address",
        "Bed",
        "Bath",
        "Sqft",
        "Windows",
        "Pets",
        "Stairs",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "PrefersSameTeam",
        "PreferredStaffId"
    ];

    private static readonly string[] BaseStaffColumns =
    [
        "StaffId",
        "StaffName",
        "Address",
        "HasPetAllergy",
        "LimitedMobility",
        "CleansWindows",
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday"
    ];

    private static readonly string[] GeocodeColumns =
    [
        "Latitude",
        "Longitude",
        "H3Cell",
        "GeocodeStatus"
    ];

    private static readonly string[] ClientEnrichmentColumns =
    [
        "EstimatedHours",
        .. GeocodeColumns
    ];

    public static PlanningScenario LoadScenario(
        string clientsCsvPath,
        string staffCsvPath,
        DateOnly weekStart)
    {
        var clientRows = LoadClientRows(clientsCsvPath);
        var staffRows = LoadStaffRows(staffCsvPath);

        var staffIds = new HashSet<string>(staffRows.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var staffRow in staffRows)
            staffIds.Add(staffRow.StaffId);

        var clients = new List<ClientPlan>(clientRows.Count);
        foreach (var clientRow in clientRows)
            clients.Add(ToClientPlan(clientRow));

        var staff = new List<StaffPlan>(staffRows.Count);
        foreach (var staffRow in staffRows)
            staff.Add(ToStaffPlan(staffRow));

        return new PlanningScenario(weekStart, clients, staff);
    }

    public static PlanningScenario LoadScenario(string clientsCsvPath, string staffCsvPath)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var offset = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = today.AddDays(-offset);
        return LoadScenario(clientsCsvPath, staffCsvPath, weekStart);
    }

    public static IReadOnlyList<ClientCsvRow> LoadClientRows(string clientsCsvPath)
    {
        using var reader = CreateCsvReader(clientsCsvPath);
        var header = reader.Header;
        var clientId = header.IndexOf("ClientId");
        var clientName = header.IndexOf("ClientName");
        var address = header.IndexOf("Address");
        var bed = header.IndexOf("Bed");
        var bath = header.IndexOf("Bath");
        var sqft = header.IndexOf("Sqft");
        var windows = header.IndexOf("Windows");
        var pets = header.IndexOf("Pets");
        var stairs = header.IndexOf("Stairs");
        var monday = header.IndexOf("Monday");
        var tuesday = header.IndexOf("Tuesday");
        var wednesday = header.IndexOf("Wednesday");
        var thursday = header.IndexOf("Thursday");
        var friday = header.IndexOf("Friday");
        var prefersSameTeam = header.IndexOf("PrefersSameTeam");
        var hasPreferredStaffId = header.TryIndexOf("PreferredStaffId", out var preferredStaffId);
        var hasEstimatedHours = header.TryIndexOf("EstimatedHours", out var estimatedHours);
        var hasLatitude = header.TryIndexOf("Latitude", out var latitude);
        var hasLongitude = header.TryIndexOf("Longitude", out var longitude);
        var hasH3Cell = header.TryIndexOf("H3Cell", out var h3Cell);
        var hasGeocodeStatus = header.TryIndexOf("GeocodeStatus", out var geocodeStatus);

        var rows = new List<ClientCsvRow>();
        foreach (var readRow in reader)
        {
            rows.Add(new ClientCsvRow(
                readRow[clientId].ToString(),
                readRow[clientName].ToString(),
                readRow[address].ToString(),
                readRow[bed].Parse<int>(),
                readRow[bath].Parse<int>(),
                readRow[sqft].Parse<int>(),
                hasEstimatedHours ? readRow[estimatedHours].TryParse<double>() : null,
                readRow[windows].Parse<bool>(),
                readRow[pets].Parse<bool>(),
                readRow[stairs].Parse<bool>(),
                readRow[monday].ToString(),
                readRow[tuesday].ToString(),
                readRow[wednesday].ToString(),
                readRow[thursday].ToString(),
                readRow[friday].ToString(),
                readRow[prefersSameTeam].Parse<bool>(),
                hasPreferredStaffId ? readRow[preferredStaffId].ToNullableString() : null,
                hasLatitude ? readRow[latitude].TryParse<double>() : null,
                hasLongitude ? readRow[longitude].TryParse<double>() : null,
                hasH3Cell ? readRow[h3Cell].ToNullableString() : null,
                hasGeocodeStatus ? readRow[geocodeStatus].ToNullableString() : null));
        }

        return rows;
    }

    public static void WriteClientRows(string clientsCsvPath, IReadOnlyList<ClientCsvRow> rows)
    {
        using var writer = CreateCsvWriter(clientsCsvPath);
        writer.Header.Add([.. BaseClientColumns, .. ClientEnrichmentColumns]);
        writer.Header.Write();

        foreach (var row in rows)
        {
            using var writeRow = writer.NewRow();
            writeRow["ClientId"].Set(row.ClientId);
            writeRow["ClientName"].Set(row.ClientName);
            writeRow["Address"].Set(row.Address);
            writeRow["Bed"].Format(row.Bed);
            writeRow["Bath"].Format(row.Bath);
            writeRow["Sqft"].Format(row.Sqft);
            writeRow["EstimatedHours"].Set(row.EstimatedHours?.ToString() ?? string.Empty);
            writeRow["Windows"].Set(row.Windows.ToString());
            writeRow["Pets"].Set(row.Pets.ToString());
            writeRow["Stairs"].Set(row.Stairs.ToString());
            writeRow["Monday"].Set(row.Monday);
            writeRow["Tuesday"].Set(row.Tuesday);
            writeRow["Wednesday"].Set(row.Wednesday);
            writeRow["Thursday"].Set(row.Thursday);
            writeRow["Friday"].Set(row.Friday);
            writeRow["PrefersSameTeam"].Set(row.PrefersSameTeam.ToString());
            writeRow["PreferredStaffId"].Set(row.PreferredStaffId ?? string.Empty);
            writeRow["Latitude"].Set(row.Latitude?.ToString() ?? string.Empty);
            writeRow["Longitude"].Set(row.Longitude?.ToString() ?? string.Empty);
            writeRow["H3Cell"].Set(row.H3Cell ?? string.Empty);
            writeRow["GeocodeStatus"].Set(row.GeocodeStatus ?? string.Empty);
        }
    }

    public static IReadOnlyList<RawStaffCsvRow> LoadStaffRows(string staffCsvPath)
    {
        using var reader = CreateCsvReader(staffCsvPath);
        var header = reader.Header;
        var staffId = header.IndexOf("StaffId");
        var staffName = header.IndexOf("StaffName");
        var address = header.IndexOf("Address");
        var hasPetAllergy = header.IndexOf("HasPetAllergy");
        var limitedMobility = header.IndexOf("LimitedMobility");
        var cleansWindows = header.IndexOf("CleansWindows");
        var monday = header.IndexOf("Monday");
        var tuesday = header.IndexOf("Tuesday");
        var wednesday = header.IndexOf("Wednesday");
        var thursday = header.IndexOf("Thursday");
        var friday = header.IndexOf("Friday");
        var hasLatitude = header.TryIndexOf("Latitude", out var latitude);
        var hasLongitude = header.TryIndexOf("Longitude", out var longitude);
        var hasH3Cell = header.TryIndexOf("H3Cell", out var h3Cell);
        var hasGeocodeStatus = header.TryIndexOf("GeocodeStatus", out var geocodeStatus);

        var rows = new List<RawStaffCsvRow>();
        foreach (var readRow in reader)
        {
            rows.Add(new RawStaffCsvRow(
                readRow[staffId].ToString(),
                readRow[staffName].ToString(),
                readRow[address].ToString(),
                readRow[hasPetAllergy].Parse<bool>(),
                readRow[limitedMobility].Parse<bool>(),
                readRow[cleansWindows].Parse<bool>(),
                readRow[monday].ToString(),
                readRow[tuesday].ToString(),
                readRow[wednesday].ToString(),
                readRow[thursday].ToString(),
                readRow[friday].ToString(),
                hasLatitude ? readRow[latitude].TryParse<double>() : null,
                hasLongitude ? readRow[longitude].TryParse<double>() : null,
                hasH3Cell ? readRow[h3Cell].ToNullableString() : null,
                hasGeocodeStatus ? readRow[geocodeStatus].ToNullableString() : null));
        }

        return rows;
    }

    public static void WriteStaffRows(
        string staffCsvPath,
        IReadOnlyList<RawStaffCsvRow> rows,
        bool includeEnrichedColumns = true)
    {
        var columns = includeEnrichedColumns
            ? [.. BaseStaffColumns, .. GeocodeColumns]
            : BaseStaffColumns;
        using var writer = CreateCsvWriter(staffCsvPath);
        writer.Header.Add(columns);
        writer.Header.Write();

        foreach (var row in rows)
        {
            using var writeRow = writer.NewRow();
            writeRow["StaffId"].Set(row.StaffId);
            writeRow["StaffName"].Set(row.StaffName);
            writeRow["Address"].Set(row.Address);
            writeRow["HasPetAllergy"].Set(row.HasPetAllergy.ToString());
            writeRow["LimitedMobility"].Set(row.LimitedMobility.ToString());
            writeRow["CleansWindows"].Set(row.CleansWindows.ToString());
            writeRow["Monday"].Set(row.Monday);
            writeRow["Tuesday"].Set(row.Tuesday);
            writeRow["Wednesday"].Set(row.Wednesday);
            writeRow["Thursday"].Set(row.Thursday);
            writeRow["Friday"].Set(row.Friday);

            if (!includeEnrichedColumns)
                continue;

            writeRow["Latitude"].Set(row.Latitude?.ToString() ?? string.Empty);
            writeRow["Longitude"].Set(row.Longitude?.ToString() ?? string.Empty);
            writeRow["H3Cell"].Set(row.H3Cell ?? string.Empty);
            writeRow["GeocodeStatus"].Set(row.GeocodeStatus ?? string.Empty);
        }
    }

    private static ClientPlan ToClientPlan(ClientCsvRow row)
    {
        return new ClientPlan(
            row.ClientId,
            row.ClientName,
            row.Address,
            row.Bed,
            row.Bath,
            row.Sqft,
            row.EstimatedHours,
            ToGeoPoint(row.Latitude, row.Longitude, row.H3Cell),
            row.Pets,
            row.Stairs,
            row.Windows,
            row.PrefersSameTeam,
            row.PreferredStaffId,
            new Schedule("", row.Monday, row.Tuesday, row.Wednesday, row.Thursday, row.Friday, ""));
    }

    private static StaffPlan ToStaffPlan(RawStaffCsvRow row)
    {
        return new StaffPlan(
            row.StaffId,
            row.StaffName,
            row.Address,
            ToGeoPoint(row.Latitude, row.Longitude, row.H3Cell),
            row.HasPetAllergy,
            row.LimitedMobility,
            row.CleansWindows,
            new Schedule("", row.Monday, row.Tuesday, row.Wednesday, row.Thursday, row.Friday, ""));
    }

    private static SepReader CreateCsvReader(string csvPath) =>
        Sep.New(',')
            .Reader(o => o with
            {
                ColNameComparer = StringComparer.OrdinalIgnoreCase,
                Trim = SepTrim.Outer,
                Unescape = true
            })
            .FromFile(csvPath);

    private static SepWriter CreateCsvWriter(string csvPath) =>
        Sep.New(',')
            .Writer(o => o with
            {
                CultureInfo = CultureInfo.InvariantCulture,
                Escape = true
            })
            .ToFile(csvPath);

    private static GeoPoint ToGeoPoint(double? latitude, double? longitude, string? h3Cell)
    {
        if (!latitude.HasValue || !longitude.HasValue)
            return new GeoPoint(0, 0, h3Cell);

        return new GeoPoint(latitude.Value, longitude.Value, h3Cell);
    }
}

static class Extensions
{
    public static string? ToNullableString(this SepReader.Col col) =>
        col.Span.IsWhiteSpace() ? null : col.ToString();
}
