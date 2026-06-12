using L5X;


var path = args.Length > 0 ? args[0] : null;
if (path is null) { Console.WriteLine("Path not Specified"); return; }
if (!File.Exists(path)) { Console.WriteLine($"File not found at {path}"); return; }

var content = new L5XReader().Read(path);





