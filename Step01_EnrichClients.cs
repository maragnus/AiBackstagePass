#:project AiBackstagePass.Common/AiBackstagePass.Common.csproj
#:package pocketken.H3
#:package NetTopologySuite
#:package Microsoft.Extensions.Hosting
#:package Microsoft.Extensions.Options
#:package Microsoft.Extensions.DependencyInjection
#:property PublishAot=false
#:property JsonSerializerIsReflectionEnabledByDefault=true

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiBackstagePass.Common;
using H3;
using H3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;

const string InputClientsCsvPath = "data/clients.csv";
const string InputStaffCsvPath = "data/staff.csv";
const string OutputClientsCsvPath = "data/clients.enriched.csv";
const string OutputStaffCsvPath = "data/staff.enriched.csv";
const string OutputClientsJsonPath = "data/clients.enriched.json";
const string OutputStaffJsonPath = "data/staff.enriched.json";
const int H3Resolution = 8;
const double MinimumEstimatedHours = 1.0;
const double EstimatedHoursRange = 3.0;
const double SqftPerUnit = 1000.0;
const double SqftWeight = 0.5;
const double WindowSqftWeight = 1.1;
const double BedWeight = 0.25;
const double BathWeight = 0.45;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddOptions<GoogleMapsOptions>().BindConfiguration("Google");

using var app = builder.Build();

// Enable API: https://console.cloud.google.com/marketplace/product/google/geocoding-backend.googleapis.com
// Create credentials (+ header button): https://console.cloud.google.com/google/maps-apis/credentials
// dotnet user-secrets set "Google:ApiKey" "..." --file "Step01_EnrichClients.cs"
var googleMapsOptions = app.Services.GetRequiredService<IOptions<GoogleMapsOptions>>().Value;
var hasGoogleCredentials = HasGoogleCredentials(googleMapsOptions);

var sourceClients = DemoScenarioFactory.LoadClientRows(InputClientsCsvPath);
var sourceStaff = DemoScenarioFactory.LoadStaffRows(InputStaffCsvPath);
var workScoreRange = GetWorkScoreRange(sourceClients);
var geocodeCache = new Dictionary<string, (double? Latitude, double? Longitude, string Status)>(StringComparer.OrdinalIgnoreCase);

using var httpClient = new HttpClient();

var enrichedClients = await EnrichRowsAsync(
    sourceClients,
    row => row.Address,
    row => row.Latitude,
    row => row.Longitude,
    row => row.H3Cell,
    row => row.GeocodeStatus,
    (row, latitude, longitude, h3Cell, geocodeStatus) => row with
    {
        EstimatedHours = CalculateEstimatedHours(row, workScoreRange.Min, workScoreRange.Max),
        Latitude = latitude,
        Longitude = longitude,
        H3Cell = h3Cell,
        GeocodeStatus = geocodeStatus
    },
    httpClient,
    googleMapsOptions,
    hasGoogleCredentials,
    geocodeCache,
    H3Resolution,
    CancellationToken.None);

var enrichedStaff = await EnrichRowsAsync(
    sourceStaff,
    row => row.Address,
    row => row.Latitude,
    row => row.Longitude,
    row => row.H3Cell,
    row => row.GeocodeStatus,
    (row, latitude, longitude, h3Cell, geocodeStatus) => row with
    {
        Latitude = latitude,
        Longitude = longitude,
        H3Cell = h3Cell,
        GeocodeStatus = geocodeStatus
    },
    httpClient,
    googleMapsOptions,
    hasGoogleCredentials,
    geocodeCache,
    H3Resolution,
    CancellationToken.None);

DemoScenarioFactory.WriteClientRows(OutputClientsCsvPath, enrichedClients);
DemoScenarioFactory.WriteStaffRows(OutputStaffCsvPath, enrichedStaff);
await WriteJsonAsync(OutputClientsJsonPath, enrichedClients, CancellationToken.None);
await WriteJsonAsync(OutputStaffJsonPath, enrichedStaff, CancellationToken.None);

var scenario = DemoScenarioFactory.LoadScenario(OutputClientsCsvPath, OutputStaffCsvPath);

Console.WriteLine($"Client rows: {enrichedClients.Count}");
Console.WriteLine($"Staff rows : {enrichedStaff.Count}");
Console.WriteLine($"Client CSV : {Path.GetFullPath(OutputClientsCsvPath)}");
Console.WriteLine($"Staff CSV  : {Path.GetFullPath(OutputStaffCsvPath)}");
Console.WriteLine($"Client JSON: {Path.GetFullPath(OutputClientsJsonPath)}");
Console.WriteLine($"Staff JSON : {Path.GetFullPath(OutputStaffJsonPath)}");
Console.WriteLine($"Scenario Clients: {scenario.Clients.Count}");
Console.WriteLine($"Scenario Staff  : {scenario.Staff.Count}");
Console.WriteLine(hasGoogleCredentials
    ? "Geocode mode: Google Maps"
    : "Geocode mode: deterministic pseudo-geocode (no Google credentials found)");

static bool HasGoogleCredentials(GoogleMapsOptions options)
    => !string.IsNullOrWhiteSpace(options.ApiKey);

static async Task<List<T>> EnrichRowsAsync<T>(
    IReadOnlyList<T> sourceRows,
    Func<T, string> getAddress,
    Func<T, double?> getLatitude,
    Func<T, double?> getLongitude,
    Func<T, string?> getH3Cell,
    Func<T, string?> getGeocodeStatus,
    Func<T, double?, double?, string?, string, T> applyGeocode,
    HttpClient httpClient,
    GoogleMapsOptions options,
    bool hasGoogleCredentials,
    IDictionary<string, (double? Latitude, double? Longitude, string Status)> cache,
    int h3Resolution,
    CancellationToken cancellationToken)
{
    var enrichedRows = new List<T>(sourceRows.Count);
    foreach (var sourceRow in sourceRows)
    {
        var geocode = await GetGeocodeAsync(
            httpClient,
            getAddress(sourceRow),
            getLatitude(sourceRow),
            getLongitude(sourceRow),
            getGeocodeStatus(sourceRow),
            options,
            hasGoogleCredentials,
            cache,
            cancellationToken);

        var h3Cell = geocode.Latitude.HasValue && geocode.Longitude.HasValue
            ? ToH3Cell(geocode.Latitude.Value, geocode.Longitude.Value, h3Resolution)
            : getH3Cell(sourceRow);

        enrichedRows.Add(applyGeocode(
            sourceRow,
            geocode.Latitude,
            geocode.Longitude,
            h3Cell,
            geocode.Status));
    }

    return enrichedRows;
}

static async Task<(double? Latitude, double? Longitude, string Status)> GetGeocodeAsync(
    HttpClient httpClient,
    string address,
    double? latitude,
    double? longitude,
    string? currentStatus,
    GoogleMapsOptions options,
    bool hasGoogleCredentials,
    IDictionary<string, (double? Latitude, double? Longitude, string Status)> cache,
    CancellationToken cancellationToken)
{
    if (latitude.HasValue && longitude.HasValue)
        return (latitude.Value, longitude.Value, currentStatus ?? "EXISTING");

    if (cache.TryGetValue(address, out var cached))
        return cached;

    var geocode = hasGoogleCredentials
        ? await GeocodeAsync(httpClient, address, options, cancellationToken)
        : PseudoGeocode(address);

    cache[address] = geocode;
    return geocode;
}

static (double? Latitude, double? Longitude, string Status) PseudoGeocode(string address)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(address));
    var latitude = 43.10 + (hash[0] / 255.0) * 2.20;
    var longitude = -70.95 + (hash[1] / 255.0) * 1.75;
    return (latitude, longitude, "PSEUDO");
}

static string ToH3Cell(double latitude, double longitude, int resolution)
{
    var coordinate = new Coordinate(longitude, latitude);
    var latLng = LatLng.FromCoordinate(coordinate);
    var index = H3Index.FromLatLng(latLng, resolution);
    return index.ToString();
}

static async Task<(double? Latitude, double? Longitude, string Status)> GeocodeAsync(
    HttpClient httpClient,
    string address,
    GoogleMapsOptions options,
    CancellationToken cancellationToken)
{
    var encodedAddress = Uri.EscapeDataString(address);
    string requestUrl;

    if (!string.IsNullOrWhiteSpace(options.ApiKey))
        requestUrl =
            $"https://maps.googleapis.com/maps/api/geocode/json?address={encodedAddress}&key={Uri.EscapeDataString(options.ApiKey)}";
    else
        return PseudoGeocode(address);

    using var response = await httpClient.GetAsync(requestUrl, cancellationToken);
    if (!response.IsSuccessStatusCode)
        return (null, null, $"HTTP_{(int)response.StatusCode}");

    await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
    using var doc = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
    var root = doc.RootElement;

    if (!root.TryGetProperty("status", out var statusElement))
        return (null, null, "MISSING_STATUS");

    var status = statusElement.GetString() ?? "UNKNOWN";
    if (!status.Equals("OK", StringComparison.OrdinalIgnoreCase))
        return (null, null, status);

    if (!root.TryGetProperty("results", out var resultsElement)
        || resultsElement.ValueKind != JsonValueKind.Array
        || resultsElement.GetArrayLength() == 0)
        return (null, null, "NO_RESULTS");

    var first = resultsElement[0];
    if (!first.TryGetProperty("geometry", out var geometry)
        || !geometry.TryGetProperty("location", out var location)
        || !location.TryGetProperty("lat", out var latitudeElement)
        || !location.TryGetProperty("lng", out var longitudeElement))
        return (null, null, "MALFORMED_RESULT");

    return (latitudeElement.GetDouble(), longitudeElement.GetDouble(), status);
}

static async Task WriteJsonAsync<T>(string path, IReadOnlyList<T> rows, CancellationToken cancellationToken)
{
    var directoryPath = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(directoryPath))
        Directory.CreateDirectory(directoryPath);

    await File.WriteAllTextAsync(
        path,
        JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
}

static (double Min, double Max) GetWorkScoreRange(IReadOnlyList<ClientCsvRow> rows)
{
    if (rows.Count == 0)
        return (0, 0);

    var min = double.MaxValue;
    var max = double.MinValue;
    foreach (var row in rows)
    {
        var score = CalculateWorkScore(row);
        if (score < min)
            min = score;
        if (score > max)
            max = score;
    }

    return (min, max);
}

static double CalculateWorkScore(ClientCsvRow row)
{
    var sqftScore = row.Sqft / SqftPerUnit;
    var score = (sqftScore * SqftWeight)
        + (row.Bed * BedWeight)
        + (row.Bath * BathWeight);

    if (row.Windows)
        score += sqftScore * WindowSqftWeight;

    return score;
}

static double CalculateEstimatedHours(ClientCsvRow row, double minScore, double maxScore)
{
    if (maxScore <= minScore)
        return MinimumEstimatedHours;

    var score = CalculateWorkScore(row);
    var scaledHours = MinimumEstimatedHours + ((score - minScore) / (maxScore - minScore)) * EstimatedHoursRange;
    var roundedHours = RoundToHalfHour(scaledHours);
    if (roundedHours < MinimumEstimatedHours)
        return MinimumEstimatedHours;

    return roundedHours;
}

static double RoundToHalfHour(double hours) =>
    Math.Round(hours * 2, MidpointRounding.AwayFromZero) / 2;

internal sealed class GoogleMapsOptions
{
    public string? ApiKey { get; set; }
}
