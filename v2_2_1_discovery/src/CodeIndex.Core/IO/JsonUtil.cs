using System.Text.Encodings.Web;
using System.Text.Json;

namespace CodeIndex.IO;

public static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
