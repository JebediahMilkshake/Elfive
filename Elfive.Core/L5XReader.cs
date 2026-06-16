using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Elfive.Core.L5X.Base;
using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace L5X;

public class L5XReader
{
    public IL5XContent? Read(string path)
    {
        return TryGetContent(path, out var content) ? content : null;
        
        /*Console.WriteLine($"Controller Name: {l5x.Controller?.Name}");
        Console.WriteLine($"Controller Type: {l5x.Controller?.ProcessorType}");
        Console.WriteLine("Content:");
        foreach (var prog in l5x.Controller?.Programs ?? [])
        {
            Console.WriteLine($"|--{prog.Name}");
            foreach (var rout in prog.Routines)
            {
                Console.WriteLine($"|----{rout.Name}");
                if (rout.Type == "Rll")
                    foreach (var rll in rout.RllContent)
                    foreach (var rung in rll.Rung)
                        Console.WriteLine($"|------{rung.Text}");
            }
        }*/
    }
    
    private bool TryGetContent(string path, out IL5XContent? content)
    {
        content = null;
        using var peekSr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        using var peekReader = XmlReader.Create(peekSr);
        peekReader.MoveToContent();
        var softwareRevision = peekReader.GetAttribute("SoftwareRevision") ?? "";
        var major = softwareRevision.Split('.') is [var m, ..] && int.TryParse(m, out var v) ? v : -1;
        
        try
        {
            content = major switch
            {
                32 => Deserialize<V32.RsLogix5000ContentType>(path),
                33 => Deserialize<V33.RsLogix5000ContentType>(path),
                34 => Deserialize<V34.RsLogix5000ContentType>(path),
                35 => Deserialize<V35.RsLogix5000ContentType>(path),
                36 => Deserialize<V36.RsLogix5000ContentType>(path),
                37 => Deserialize<V37.RsLogix5000ContentType>(path),
                _ => throw new NotSupportedException($"L5X schema version {major} is not supported")
            };
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }
    
    private static IL5XContent Deserialize<T>(string path) where T : IL5XContent
    {
        var serializer = new XmlSerializer(typeof(T));
        using var sr = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        return (T)serializer.Deserialize(sr)!;
    }
}
