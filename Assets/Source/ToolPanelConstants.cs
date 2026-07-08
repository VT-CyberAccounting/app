public static class ToolPanelConstants
{
    public static readonly ToolType[] Tools =
    {
        ToolType.Inspect,
        ToolType.Compare,
        ToolType.Slice,
        ToolType.Grab,
        ToolType.Color
    };

    public static string Label(ToolType tool) => tool.ToString();

    public static string Hint(ToolType tool) => $"This button opens the {tool} tool.";

    public static string Description(ToolType tool)
    {
        switch (tool)
        {
            case ToolType.Inspect:
                return "The Inspect tool allows you to highlight a sheet's columns or rows by hovering over them.";
            case ToolType.Compare:
                return "The Compare tool allows you to compare a cell or sheet by selecting them.";
            case ToolType.Slice:
                return "The Slice tool allows you to break apart a sheet along its columns or rows axis by clicking on them.";
            case ToolType.Color:
                return "The Color tool allows you to change a sheet's color by selecting a color and clicking on it.";
            case ToolType.Grab:
                return "The Grab tool allows you to move or rotate a sheet by using one or two hands.";
            default:
                return string.Empty;
        }
    }
}
