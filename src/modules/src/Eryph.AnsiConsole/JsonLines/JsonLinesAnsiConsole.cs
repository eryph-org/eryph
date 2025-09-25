using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Eryph.AnsiConsole.JsonLines;

public class JsonLinesAnsiConsole : IJsonLinesAnsiConsole
{
    private static readonly Lazy<JsonSerializerOptions> LazyOptions = new(() =>
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        });

    private readonly IAnsiConsole _outputConsole;
    private readonly IAnsiConsole _innerConsole;
    private readonly StringWriter _writer;

    public JsonLinesAnsiConsole(IAnsiConsole outputConsole)
    {
        _outputConsole = outputConsole;
        _writer = new StringWriter();
        var stringBufferOutput = new AnsiConsoleOutput(_writer);
        _innerConsole = Spectre.Console.AnsiConsole.Create(new AnsiConsoleSettings
        {
            Interactive = InteractionSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Ansi = AnsiSupport.No,
            Out = stringBufferOutput,
        });
    }

    public Profile Profile => _innerConsole.Profile;

    public IAnsiConsoleCursor Cursor => _innerConsole.Cursor;

    public IAnsiConsoleInput Input => _innerConsole.Input;

    public IExclusivityMode ExclusivityMode => _innerConsole.ExclusivityMode;

    public RenderPipeline Pipeline => _innerConsole.Pipeline;

    public void Clear(bool home) => _innerConsole.Clear(home);

    public void Write(IRenderable renderable)
    {
        _innerConsole.Write(renderable);

        var result = _writer.ToString();
        _writer.GetStringBuilder().Clear();

        if (string.IsNullOrWhiteSpace(result))
            return;

        var jsonLine = new JsonLineInfo
        {
            Message = result.Trim().ReplaceLineEndings("\n"),
        };

        var json = Serialize(jsonLine);
        _outputConsole.Profile.Out.Writer.WriteLine(json);
    }

    public void WriteError(int code, string message)
    {
        var jsonLineResult = new JsonLineError
        {
            ExitCode = code,
            Error = message.ReplaceLineEndings("\n"),
        };
        var json = Serialize(jsonLineResult);
        _outputConsole.Profile.Out.Writer.WriteLine(json);
    }

    public void WriteResult(string? result)
    {
        var jsonLineResult = new JsonLineResult
        {
            ExitCode = 0,
            Result = result?.ReplaceLineEndings("\n")
        };
        var json = Serialize(jsonLineResult);
        _outputConsole.Profile.Out.Writer.WriteLine(json);
    }

    private static string Serialize(JsonLineOutput jsonLineOutput) =>
        JsonSerializer.Serialize(jsonLineOutput, LazyOptions.Value);
}
