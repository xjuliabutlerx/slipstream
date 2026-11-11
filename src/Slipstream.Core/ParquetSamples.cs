using Parquet;
using Parquet.Serialization;

namespace Slipstream.Core;

public static class ParquetSamples
{
    public static async Task WriteAsync(string path, IEnumerable<ChannelSample> samples, int rowGroupSize = 100_000)
    {
        var rows = samples.Select(s => new ChannelSampleRow {SessionSeconds = s.SessionTime.TotalSeconds, Car = s.Car, Channel = s.Channel, Value = s.Value, Unit = s.Unit});

        await using var stream = File.Create(path);

        await ParquetSerializer.SerializeAsync(rows, stream, new ParquetOptions { RowGroupSize = rowGroupSize });
    }

    public static async Task<List<ChannelSample>> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var result = await ParquetSerializer.DeserializeAsync<ChannelSampleRow>(stream);

        return result.Data
            .Select(r => new ChannelSample(TimeSpan.FromSeconds(r.SessionSeconds), r.Car, r.Channel, r.Value, r.Unit))
            .ToList();
    }
}