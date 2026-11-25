using Farq;

var v1 = """
public sealed class Greeter
{
    public string Greet(string name)
    {
        return "Hello, " + name;
    }
}
""";

var v2 = """
public sealed class Greeter
{
    public string Greet(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required", nameof(name));
        return $"Hello, {name}!";
    }
}
""";

Console.WriteLine("diff Greeter.v1.cs -> Greeter.v2.cs");
Console.WriteLine();

var patch = UnifiedDiff.Format(Diff.Compute(v1, v2), "Greeter.v1.cs", "Greeter.v2.cs");
foreach (var line in patch.Split('\n'))
{
    var (bg, fg) = line switch
    {
        _ when line.StartsWith("+++") || line.StartsWith("---") => ("\x1b[0m", "\x1b[1m"),
        _ when line.StartsWith("@@") => ("\x1b[0m", "\x1b[36m"),
        _ when line.StartsWith('+') => ("\x1b[0m", "\x1b[32m"),
        _ when line.StartsWith('-') => ("\x1b[0m", "\x1b[31m"),
        _ => ("\x1b[0m", "\x1b[90m"),
    };
    Console.WriteLine($"{bg}{fg}{line}\x1b[0m");
}

var rebuilt = UnifiedDiff.Apply(v1, patch);
Console.WriteLine();
Console.WriteLine(rebuilt == v2
    ? "patch applied cleanly: result matches Greeter.v2.cs byte for byte"
    : "MISMATCH");
