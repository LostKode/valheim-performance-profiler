using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ValheimPerformanceProfiler;

internal sealed class JsonLinesReport : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public JsonLinesReport(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Write(string type, params (string Key, object? Value)[] fields)
    {
        var builder = new StringBuilder(256);
        builder.Append("{\"type\":").Append(Quote(type));
        builder.Append(",\"utc\":").Append(Quote(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        foreach (var field in fields) builder.Append(',').Append(Quote(field.Key)).Append(':').Append(Value(field.Value));
        builder.Append('}');
        lock (_gate) _writer.WriteLine(builder.ToString());
    }

    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 32) builder.Append("\\u").Append(((int)character).ToString("x4"));
                    else builder.Append(character);
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    private static string Value(object? value)
    {
        if (value == null) return "null";
        if (value is string text) return Quote(text);
        if (value is bool boolean) return boolean ? "true" : "false";
        if (value is float or double or decimal) return Convert.ToDouble(value).ToString("0.###", CultureInfo.InvariantCulture);
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong) return Convert.ToString(value, CultureInfo.InvariantCulture)!;
        return Quote(value.ToString() ?? string.Empty);
    }

    public void Dispose() => _writer.Dispose();
}

