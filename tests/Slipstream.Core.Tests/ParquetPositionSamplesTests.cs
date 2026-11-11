namespace Slipstream.Core.Tests;

public class ParquetPositionSamplesTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsEveryField()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".parquet");
        try
        {
            var samples = new List<PositionSample>
            {
                new(TimeSpan.FromSeconds(-2.5), "44", 100.0, -250.0, 2227.0, "OnTrack"),
                new(TimeSpan.FromSeconds(0.25), "1",  -3.5,  48.2,   2228.1, "OffTrack"),
                new(TimeSpan.FromSeconds(3600), "63", 0.1,   0.2,    0.3,    "OnTrack"),
            };

            await ParquetPositionSamples.WriteAsync(path, samples);
            var back = await ParquetPositionSamples.ReadAsync(path);

            Assert.Equal(samples.Count, back.Count);
            for (var i = 0; i < samples.Count; i++)
            {
                Assert.Equal(samples[i].SessionTime.TotalSeconds, back[i].SessionTime.TotalSeconds, precision: 6);
                Assert.Equal(samples[i].Car,    back[i].Car);
                Assert.Equal(samples[i].X,      back[i].X);
                Assert.Equal(samples[i].Y,      back[i].Y);
                Assert.Equal(samples[i].Z,      back[i].Z);
                Assert.Equal(samples[i].Status, back[i].Status);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteThenRead_SpanningSeveralRowGroups_KeepsAllRowsInOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".parquet");
        try
        {
            // 25 rows with a row group size of 10: groups of 10, 10 and 5.
            var samples = Enumerable.Range(0, 25)
                .Select(i => new PositionSample(TimeSpan.FromSeconds(i), "44", i, i * 2, i * 3, "OnTrack"))
                .ToList();

            await ParquetPositionSamples.WriteAsync(path, samples, rowGroupSize: 10);
            var back = await ParquetPositionSamples.ReadAsync(path);

            Assert.Equal(25, back.Count);
            Assert.Equal(samples.Select(s => s.X), back.Select(s => s.X));
            Assert.Equal(samples.Select(s => s.Y), back.Select(s => s.Y));
            Assert.Equal(samples.Select(s => s.Z), back.Select(s => s.Z));
        }
        finally
        {
            File.Delete(path);
        }
    }
}