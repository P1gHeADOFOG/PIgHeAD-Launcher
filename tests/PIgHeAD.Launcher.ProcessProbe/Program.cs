using System.Text.Json;

// Test-only executable that mimics Java's version probe and echoes launch arguments.
if (args is ["-version"])
{
    Console.Error.WriteLine("openjdk version \"21.0.1\"");
}
else
{
    Console.WriteLine(JsonSerializer.Serialize(args));
    Console.Error.WriteLine("test diagnostic output");
}
