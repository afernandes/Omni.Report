using System.Runtime.InteropServices;
internal static class GuiResources
{
    [DllImport("user32.dll")] internal static extern uint GetGuiResources(nint process, uint flag);
}
