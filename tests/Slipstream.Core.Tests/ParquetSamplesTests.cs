namespace Slipstream.Core.Tests;

public class ParquetSamplesTests
{
    [Fact]
    public async Task WriteThenRead_RoundTripsEveryField()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".parquet");
        try
        {
            var samples = new List<ChannelSample>
            {
                new(TimeSpan.FromSeconds(-2.5),  "44", "speed",  280, "km/h"),
                new(TimeSpan.FromSeconds(0.25),  "1",  "n_gear", 7,   "gear"),
                new(TimeSpan.FromSeconds(3600),  "63", "brake",  1,   "on/off"),
            };

            await ParquetSamples.WriteAsync(path, samples);
            var back = await ParquetSamples.ReadAsync(path);

            Assert.Equal(samples.Count, back.Count);
            for (var i = 0; i < samples.Count; i++)
            {
                Assert.Equal(samples[i].SessionTime.TotalSeconds, back[i].SessionTime.TotalSeconds, precision: 6);
                Assert.Equal(samples[i].Car,     back[i].Car);
                Assert.Equal(samples[i].Channel, back[i].Channel);
                Assert.Equal(samples[i].Value,   back[i].Value);
                Assert.Equal(samples[i].Unit,    back[i].Unit);
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
                .Select(i => new ChannelSample(TimeSpan.FromSeconds(i), "44", "speed", i, "km/h"))
                .ToList();

            await ParquetSamples.WriteAsync(path, samples, rowGroupSize: 10);
            var back = await ParquetSamples.ReadAsync(path);

            Assert.Equal(25, back.Count);
            Assert.Equal(samples.Select(s => s.Value), back.Select(s => s.Value));
        }
        finally
        {
            File.Delete(path);
        }
    }
}