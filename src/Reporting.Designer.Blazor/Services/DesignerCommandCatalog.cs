using Reporting.Designer.Blazor.ViewModels;

namespace Reporting.Designer.Blazor.Services;

/// <summary>The Designer's command list. Every id here is routed by <c>ReportDesigner.OnMenuCommand</c> (a test runs
/// all of them), and every shortcut listed is one the keyboard handler implements.</summary>
internal static class DesignerCommandCatalog
{
    /// <summary>Top-level menus, left to right.</summary>
    internal static readonly string[] Menus = ["Arquivo", "Editar", "Visualizar", "Inserir", "Organizar", "Relatório", "Ajuda"];

    /// <summary>Keys handled on the canvas that are not commands of their own, listed in the shortcuts dialog.</summary>
    internal static readonly (string Keys, string Description)[] CanvasKeys =
    [
        ("Setas", "Mover a seleção 1 mm"),
        ("Shift+Setas", "Mover a seleção 10 mm"),
        ("F8", "Abrir o editor de expressões"),
        ("Esc", "Sair do preview / fechar diálogo"),
        ("Ctrl+O", "Abrir arquivo .repx"),
        ("Ctrl+K", "Paleta de comandos"),
    ];

    /// <summary>Builds the list for this host: the fixed commands plus one export per registered exporter.</summary>
    internal static IReadOnlyList<DesignerCommand> Build(IReadOnlyList<ExporterDescriptor> exporters)
    {
        var commands = new List<DesignerCommand>
        {
            new("file.new", "Novo", "Arquivo", "plus"),
            new("file.open", "Abrir .repx…", "Arquivo", "folder-open", "Ctrl+O"),
            new("file.save", "Salvar", "Arquivo", "save", "Ctrl+S"),
            new("file.saveas", "Salvar como…", "Arquivo", "save", "Ctrl+Shift+S"),
        };
        for (int i = 0; i < exporters.Count; i++)
        {
            commands.Add(new("file.export." + exporters[i].Format, "Exportar " + exporters[i].Label, "Arquivo",
                exporters[i].IconName, StartsGroup: i == 0));
        }
        commands.Add(new("file.export.rdl", "Exportar RDL (.rdl)", "Arquivo", "file-code", StartsGroup: true));
        commands.Add(new("file.print", "Imprimir", "Arquivo", "printer", "Ctrl+P", StartsGroup: true));

        commands.AddRange(
        [
            new("edit.undo", "Desfazer", "Editar", "undo-2", "Ctrl+Z"),
            new("edit.redo", "Refazer", "Editar", "redo-2", "Ctrl+Y"),
            new("edit.cut", "Recortar", "Editar", "scissors", "Ctrl+X", StartsGroup: true),
            new("edit.copy", "Copiar", "Editar", "copy", "Ctrl+C"),
            new("edit.paste", "Colar", "Editar", "clipboard-paste", "Ctrl+V"),
            new("edit.duplicate", "Duplicar", "Editar", "copy", "Ctrl+D"),
            new("edit.delete", "Excluir", "Editar", "trash-2", "Del"),
            new("edit.selectall", "Selecionar tudo na banda", "Editar", "box-select", "Ctrl+A", StartsGroup: true),

            new("view.grid", "Alternar grade", "Visualizar", "grid-3x3"),
            new("view.snap", "Alternar snap", "Visualizar", "magnet"),
            new("view.rulers", "Alternar régua", "Visualizar", "ruler"),
            new("view.theme", "Alternar tema", "Visualizar", "moon", "Ctrl+Shift+L"),
            new("view.zoomin", "Aumentar zoom", "Visualizar", "plus", StartsGroup: true),
            new("view.zoomout", "Diminuir zoom", "Visualizar", "minus"),
            new("view.zoom100", "Zoom 100%", "Visualizar", "search"),
        ]);

        bool firstGroup = true;
        foreach (var group in ToolboxCatalog.Groups)
        {
            for (int i = 0; i < group.Items.Count; i++)
            {
                var item = group.Items[i];
                commands.Add(new("insert." + item.Kind.ToString().ToLowerInvariant(), item.Label, "Inserir", item.Icon,
                    StartsGroup: i == 0 && !firstGroup));
            }
            firstGroup = false;
        }

        commands.AddRange(
        [
            new("arrange.align.left", "Alinhar à esquerda", "Organizar", "align-start-vertical"),
            new("arrange.align.centerh", "Centralizar horizontal", "Organizar", "align-center-vertical"),
            new("arrange.align.right", "Alinhar à direita", "Organizar", "align-end-vertical"),
            new("arrange.align.top", "Alinhar topo", "Organizar", "align-start-horizontal", StartsGroup: true),
            new("arrange.align.middlev", "Centralizar vertical", "Organizar", "align-center-horizontal"),
            new("arrange.align.bottom", "Alinhar base", "Organizar", "align-end-horizontal"),
            new("arrange.distribute.h", "Distribuir horizontalmente", "Organizar", "columns-3", StartsGroup: true),
            new("arrange.distribute.v", "Distribuir verticalmente", "Organizar", "rows-3"),
            new("arrange.size.width", "Mesma largura", "Organizar", "move-horizontal", StartsGroup: true),
            new("arrange.size.height", "Mesma altura", "Organizar", "move-vertical"),
            new("arrange.size.both", "Mesmo tamanho", "Organizar", "square"),
            new("arrange.order.front", "Trazer para a frente", "Organizar", "chevrons-up", StartsGroup: true),
            new("arrange.order.forward", "Avançar uma camada", "Organizar", "chevron-up"),
            new("arrange.order.backward", "Recuar uma camada", "Organizar", "chevron-down"),
            new("arrange.order.back", "Enviar para trás", "Organizar", "chevrons-down"),

            new("report.pagesetup", "Configuração de página…", "Relatório", "file-cog"),
            new("view.preview", "Preview", "Relatório", "play", "F5"),
            new("report.validate", "Validar expressões", "Relatório", "check-circle-2"),

            new("help.shortcuts", "Atalhos de teclado", "Ajuda", "keyboard", "Ctrl+/"),
            new("help.about", "Sobre OmniReport…", "Ajuda", "info"),
        ]);
        return commands;
    }
}
