using AiBackstagePass.Common;

namespace AiBackstagePass.Common.Tests;

public sealed class DemoScenarioFactoryTests
{
    [Fact]
    public void LoadClientRows_WithoutEnrichedColumns_SetsOptionalFieldsToNull()
    {
        var clientsCsv = string.Join('\n',
            "ClientId,ClientName,Address,Bed,Bath,Sqft,Windows,Pets,Stairs,Monday,Tuesday,Wednesday,Thursday,Friday,PrefersSameTeam,PreferredStaffId",
            "C001,Ada,1 Main St,2,1,900,True,False,False,M,N,A,M,N,False,");

        var testDirectory = CreateTestDirectory();
        var clientsPath = Path.Combine(testDirectory, "clients.csv");
        File.WriteAllText(clientsPath, clientsCsv);

        try
        {
            var rows = DemoScenarioFactory.LoadClientRows(clientsPath);

            Assert.Single(rows);
            var row = rows[0];
            Assert.Null(row.Latitude);
            Assert.Null(row.Longitude);
            Assert.Null(row.H3Cell);
            Assert.Null(row.GeocodeStatus);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadStaffRows_WithoutEnrichedColumns_SetsOptionalFieldsToNull()
    {
        var staffCsv = string.Join('\n',
            "StaffId,StaffName,Address,HasPetAllergy,LimitedMobility,CleansWindows,Monday,Tuesday,Wednesday,Thursday,Friday",
            "S001,Alex,1 Oak St,False,False,True,M,N,A,M,N");

        var testDirectory = CreateTestDirectory();
        var staffPath = Path.Combine(testDirectory, "staff.csv");
        File.WriteAllText(staffPath, staffCsv);

        try
        {
            var rows = DemoScenarioFactory.LoadStaffRows(staffPath);

            Assert.Single(rows);
            var row = rows[0];
            Assert.Null(row.Latitude);
            Assert.Null(row.Longitude);
            Assert.Null(row.H3Cell);
            Assert.Null(row.GeocodeStatus);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void LoadScenario_WithOptionalEnrichment_PopulatesLocationsWhenPresent()
    {
        var clientsCsv = string.Join('\n',
            "ClientId,ClientName,Address,Bed,Bath,Sqft,Windows,Pets,Stairs,Monday,Tuesday,Wednesday,Thursday,Friday,PrefersSameTeam,PreferredStaffId,Latitude,Longitude,H3Cell,GeocodeStatus",
            "C001,Ada,1 Main St,2,1,900,True,False,False,M,N,A,M,N,False,,43.123456,-70.654321,882a1340b9fffff,OK",
            "C002,Bob,2 Main St,3,2,1200,False,True,True,N,M,N,A,M,True,S01,,,," );

        var staffCsv = string.Join('\n',
            "StaffId,StaffName,Address,HasPetAllergy,LimitedMobility,CleansWindows,Monday,Tuesday,Wednesday,Thursday,Friday,Latitude,Longitude,H3Cell,GeocodeStatus",
            "S01,Alex,10 Oak St,False,False,True,M,M,M,M,M,43.223456,-70.754321,882a1340a1fffff,OK",
            "S02,Sam,20 Pine St,True,False,False,N,N,N,N,N,,,," );

        var testDirectory = CreateTestDirectory();
        var clientsPath = Path.Combine(testDirectory, "clients.csv");
        var staffPath = Path.Combine(testDirectory, "staff.csv");
        File.WriteAllText(clientsPath, clientsCsv);
        File.WriteAllText(staffPath, staffCsv);

        try
        {
            var weekStart = new DateOnly(2026, 2, 23);
            var scenario = DemoScenarioFactory.LoadScenario(clientsPath, staffPath, weekStart);

            Assert.Equal(2, scenario.Clients.Count);
            Assert.Equal(43.123456, scenario.Clients[0].Location.Latitude, 6);
            Assert.Equal(-70.654321, scenario.Clients[0].Location.Longitude, 6);
            Assert.Equal("882a1340b9fffff", scenario.Clients[0].Location.H3Cell);

            Assert.Equal(0, scenario.Clients[1].Location.Latitude);
            Assert.Equal(0, scenario.Clients[1].Location.Longitude);
            Assert.Null(scenario.Clients[1].Location.H3Cell);

            Assert.Equal(2, scenario.Staff.Count);
            Assert.Equal("S01", scenario.Staff[0].StaffId);
            Assert.Equal(43.223456, scenario.Staff[0].Location.Latitude, 6);
            Assert.Equal(-70.754321, scenario.Staff[0].Location.Longitude, 6);
            Assert.Equal("882a1340a1fffff", scenario.Staff[0].Location.H3Cell);

            Assert.Equal("S02", scenario.Staff[1].StaffId);
            Assert.Equal(0, scenario.Staff[1].Location.Latitude);
            Assert.Equal(0, scenario.Staff[1].Location.Longitude);
            Assert.Null(scenario.Staff[1].Location.H3Cell);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteClientRows_WithOptionalEnrichment_RoundTripsValues()
    {
        var rows = new List<ClientCsvRow>
        {
            new(
                "C001",
                "Ada",
                "1 Main St",
                2,
                1,
                900,
                true,
                false,
                false,
                "M",
                "N",
                "A",
                "M",
                "N",
                false,
                null,
                43.123456,
                -70.654321,
                "882a1340b9fffff",
                "OK"),
            new(
                "C002",
                "Bob",
                "2 Main St",
                3,
                2,
                1200,
                false,
                true,
                true,
                "N",
                "M",
                "N",
                "A",
                "M",
                true,
                "S01",
                null,
                null,
                null,
                null)
        };

        var testDirectory = CreateTestDirectory();
        var clientsPath = Path.Combine(testDirectory, "clients.csv");

        try
        {
            DemoScenarioFactory.WriteClientRows(clientsPath, rows);
            var loaded = DemoScenarioFactory.LoadClientRows(clientsPath);

            Assert.Equal(rows.Count, loaded.Count);
            Assert.Equal(rows[0], loaded[0]);
            Assert.Equal(rows[1], loaded[1]);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteStaffRows_WithOptionalEnrichment_RoundTripsValues()
    {
        var rows = new List<RawStaffCsvRow>
        {
            new(
                "S001",
                "Alex",
                "1 Oak St",
                false,
                false,
                true,
                "M",
                "N",
                "A",
                "M",
                "N",
                43.223456,
                -70.754321,
                "882a1340a1fffff",
                "OK"),
            new(
                "S002",
                "Sam",
                "2 Pine St",
                true,
                false,
                false,
                "N",
                "A",
                "M",
                "N",
                "A",
                null,
                null,
                null,
                null)
        };

        var testDirectory = CreateTestDirectory();
        var staffPath = Path.Combine(testDirectory, "staff.csv");

        try
        {
            DemoScenarioFactory.WriteStaffRows(staffPath, rows, includeEnrichedColumns: true);
            var loaded = DemoScenarioFactory.LoadStaffRows(staffPath);

            Assert.Equal(rows.Count, loaded.Count);
            Assert.Equal(rows[0], loaded[0]);
            Assert.Equal(rows[1], loaded[1]);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "AiBackstagePass.Common.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }
}
