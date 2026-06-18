using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Elfive.Core.L5X.Base;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace L5X;

public class L5XReader
{
    public IL5XContent Read(string path)
    {
        using var peekSr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        using var peekReader = XmlReader.Create(peekSr);
        peekReader.MoveToContent();
        var softwareRevision = peekReader.GetAttribute("SoftwareRevision") ?? "";
        var major = softwareRevision.Split('.') is [var m, ..] && int.TryParse(m, out var v) ? v : -1;

        if (major < 0)
            throw new NotSupportedException(
                "Could not determine the L5X schema version. The file may not be a valid L5X export.");

        return major switch
        {
            32 => Deserialize<V32.RsLogix5000ContentType>(path),
            33 => Deserialize<V33.RsLogix5000ContentType>(path),
            34 => Deserialize<V34.RsLogix5000ContentType>(path),
            35 => Deserialize<V35.RsLogix5000ContentType>(path),
            36 => Deserialize<V36.RsLogix5000ContentType>(path),
            37 => Deserialize<V37.RsLogix5000ContentType>(path),
            _ => throw new NotSupportedException($"L5X schema version {major} is not supported. Supported versions: 32–37.")
        };
    }
    
    private static IL5XContent Deserialize<T>(string path) where T : IL5XContent
    {
        var serializer = new XmlSerializer(typeof(T));
        using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        return (T)serializer.Deserialize(sr)!;
    }
}
