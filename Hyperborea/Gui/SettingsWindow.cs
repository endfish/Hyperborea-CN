using Dalamud.Interface.Components;
using ECommons.SimpleGui;
using Lumina.Excel.Sheets;

namespace Hyperborea.Gui;
public class SettingsWindow : Window
{
    public SettingsWindow() : base(Strings.SettingsWindowTitle)
    {
        EzConfigGui.WindowSystem.AddWindow(this);
    }

    public override void Draw()
    {
        if (ImGuiGroup.BeginGroupBox("常规设置"))
        {
            ImGuiEx.Text("坐骑：");
            ImGuiEx.SetNextItemFullWidth(-10);
            if (ImGui.BeginCombo("##mount", Utils.GetMountName(C.CurrentMount) ?? Strings.SelectMount))
            {
                ImGui.SetNextItemWidth(150f);
                ImGui.InputTextWithHint("##search", Strings.Filter, ref UI.MountFilter, 50);
                if (ImGui.Selectable(Strings.NoMount))
                {
                    C.CurrentMount = 0;
                }
                foreach (var x in Svc.Data.GetExcelSheet<Mount>())
                {
                    var name = Utils.GetMountName(x.RowId);
                    if (!name.IsNullOrEmpty())
                    {
                        if (UI.MountFilter.IsNullOrEmpty() || name.Contains(UI.MountFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            if (ImGui.Selectable(name))
                            {
                                C.CurrentMount = x.RowId;
                            }
                        }
                    }
                }
                ImGui.EndCombo();
            }
            ImGuiGroup.EndGroupBox();
        }

        if (ImGuiGroup.BeginGroupBox("危险区域", EColor.RedBright.ToUint()))
        {
            if (P.Enabled) ImGui.BeginDisabled();
            ImGui.Checkbox("禁用区域限制", ref C.DisableInnCheck);
            ImGuiComponents.HelpMarker($"移除 {Strings.PluginName} 只能在{Strings.InnRoomExample}内使用的限制。如果你在公共区域使用时数据包过滤出现异常，可能会有风险。");
            if (P.Enabled)
            {
                ImGui.EndDisabled();
                ImGuiEx.TextWrapped(EColor.RedBright, "插件启用期间无法修改这些设置。");
            }
            ImGuiGroup.EndGroupBox();
        }

        
    }
}
