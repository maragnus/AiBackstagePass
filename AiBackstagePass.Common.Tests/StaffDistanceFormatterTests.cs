using AiBackstagePass.Common;

namespace AiBackstagePass.Common.Tests;

public sealed class StaffDistanceFormatterTests
{
    [Fact]
    public void BuildDistancesToOtherStaffMiles_SortsByDistanceAndFormats()
    {
        var staff = new List<RawStaffCsvRow>
        {
            new(
                "S1",
                "Alpha",
                "1 Main St",
                false,
                false,
                false,
                "A",
                "A",
                "A",
                "A",
                "A",
                0,
                0,
                null,
                null,
                null),
            new(
                "S2",
                "Beta",
                "2 Main St",
                false,
                false,
                false,
                "A",
                "A",
                "A",
                "A",
                "A",
                0,
                1,
                null,
                null,
                null),
            new(
                "S3",
                "Gamma",
                "3 Main St",
                false,
                false,
                false,
                "A",
                "A",
                "A",
                "A",
                "A",
                0,
                2,
                null,
                null,
                null)
        };

        var result = StaffDistanceFormatter.BuildDistancesToOtherStaffMiles(staff, staff[0], decimalPlaces: 1);

        Assert.Equal("S2=69.1|S3=138.2", result);
    }

    [Fact]
    public void BuildDistancesToOtherStaffMiles_MissingLocation_ReturnsEmpty()
    {
        var staff = new List<RawStaffCsvRow>
        {
            new(
                "S1",
                "Alpha",
                "1 Main St",
                false,
                false,
                false,
                "A",
                "A",
                "A",
                "A",
                "A",
                null,
                null,
                null,
                null,
                null),
            new(
                "S2",
                "Beta",
                "2 Main St",
                false,
                false,
                false,
                "A",
                "A",
                "A",
                "A",
                "A",
                0,
                1,
                null,
                null,
                null)
        };

        var result = StaffDistanceFormatter.BuildDistancesToOtherStaffMiles(staff, staff[0]);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void MilesBetween_ReturnsExpectedDistance()
    {
        var miles = StaffDistanceFormatter.MilesBetween(0, 0, 0, 1);

        Assert.Equal(69.1, miles, 1);
    }
}
