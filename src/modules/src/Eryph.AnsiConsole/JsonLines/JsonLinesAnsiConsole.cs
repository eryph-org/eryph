using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Eryph.AnsiConsole.JsonLines;

public class JsonLinesAnsiConsole : IAnsiConsole
{
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
            Message = result.Trim(),
        };

        var json = JsonLinesSerializer.Serialize(jsonLine);
        _outputConsole.WriteLine(json);
    }
}
