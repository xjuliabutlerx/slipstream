using Parquet;
using Parquet.Serialization;

namespace Slipstream.Core;

public static class ParquetPositionSamples
{
    public static async Task WriteAsync(string path, IEnumerable<PositionSample> samples, int rowGroupSize = 100_000)
    {
        var rows = samples.Select(s => new PositionSampleRow {SessionSeconds = s.SessionTime.TotalSeconds, Car = s.Car, X = s.X, Y = s.Y, Z = s.Z, Status = s.Status});

        await using var stream = File.Create(path);

        await ParquetSerializer.SerializeAsync(rows, stream, new ParquetOptions { RowGroupSize = rowGroupSize });
    }

    public static async Task<List<PositionSample>> ReadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var result = await ParquetSerializer.DeserializeAsync<PositionSampleRow>(stream);

        return result.Data
            .Select(r => new PositionSample(TimeSpan.FromSeconds(r.SessionSeconds), r.Car, r.X, r.Y, r.Z, r.Status))
            .ToList();
    }
}