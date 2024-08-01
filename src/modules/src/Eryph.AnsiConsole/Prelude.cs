using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Eryph.AnsiConsole;

public static class Prelude
{
    public static IRenderable Renderable(Error error) =>
        addToGrid(createGrid(), error);

    private static Grid addRow(Grid grid, IRenderable renderable) =>
        grid.AddRow(new Markup(""), renderable);

    private static Grid addToGrid(Grid grid, Error error) => error switch
    {
        ManyErrors me => me.Errors.Fold(grid, addToGrid),
        Exceptional ee => addRow(grid, ee.ToException().GetRenderable()),
        _ => addRow(grid, new Text(error.Message))
                .Apply(g => error.Inner.Match(
                    Some: ie => addRow(g, addToGrid(createGrid(), ie)),
                    None: () => g)),
    };

    private static Grid createGrid() => new Grid()
        .AddColumn(new GridColumn() { Width = 2 })
        .AddColumn();
}