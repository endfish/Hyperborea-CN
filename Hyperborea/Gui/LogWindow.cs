using ECommons.SimpleGui;

namespace Hyperborea.Gui;
public class LogWindow : Window
{
    public LogWindow() : base(Strings.LogWindowTitle)
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        InternalLog.PrintImgui();
    }
}
