using System;
using System.Linq;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace BetterFollowbotLite;

internal class ImGuiDrawSettings
{

    internal static void DrawImGuiSettings()
    {
        var green = new Vector4(0.102f, 0.388f, 0.106f, 1.000f);
        var red = new Vector4(0.388f, 0.102f, 0.102f, 1.000f);

        var collapsingHeaderFlags = ImGuiTreeNodeFlags.CollapsingHeader;
        ImGui.Text("Plugin by alekswoje (forked from Totalschaden). https://github.com/alekswoje/BetterFollowbotLite");

        try
        {
            // Input Keys
            ImGui.PushStyleColor(ImGuiCol.Header, green);
            ImGui.PushID(1000);
            if (ImGui.TreeNodeEx("Input Keys", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.inputKey1.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 2 Key: " + BetterFollowbotLite.Instance.Settings.inputKey1.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey1.Value);
                BetterFollowbotLite.Instance.Settings.inputKey3.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 4 Key: " + BetterFollowbotLite.Instance.Settings.inputKey3.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey3.Value);
                BetterFollowbotLite.Instance.Settings.inputKey4.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 5 Key: " + BetterFollowbotLite.Instance.Settings.inputKey4.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey4.Value);
                BetterFollowbotLite.Instance.Settings.inputKey5.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 6 Key: " + BetterFollowbotLite.Instance.Settings.inputKey5.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey5.Value);
                BetterFollowbotLite.Instance.Settings.inputKey6.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 7 Key: " + BetterFollowbotLite.Instance.Settings.inputKey6.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey6.Value);
                BetterFollowbotLite.Instance.Settings.inputKey7.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 8 Key: " + BetterFollowbotLite.Instance.Settings.inputKey7.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey7.Value);
                BetterFollowbotLite.Instance.Settings.inputKey8.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 9 Key: " + BetterFollowbotLite.Instance.Settings.inputKey8.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey8.Value);
                BetterFollowbotLite.Instance.Settings.inputKey9.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 10 Key: " + BetterFollowbotLite.Instance.Settings.inputKey9.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey9.Value);
                BetterFollowbotLite.Instance.Settings.inputKey10.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 11 Key: " + BetterFollowbotLite.Instance.Settings.inputKey10.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey10.Value);
                BetterFollowbotLite.Instance.Settings.inputKey11.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 12 Key: " + BetterFollowbotLite.Instance.Settings.inputKey11.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey11.Value);
                BetterFollowbotLite.Instance.Settings.inputKey12.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 13 Key: " + BetterFollowbotLite.Instance.Settings.inputKey12.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey12.Value);
                BetterFollowbotLite.Instance.Settings.inputKeyPickIt.Value = ImGuiExtension.HotkeySelector(
                    "PickIt Key: " + BetterFollowbotLite.Instance.Settings.inputKeyPickIt.Value,
                    BetterFollowbotLite.Instance.Settings.inputKeyPickIt.Value);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }


        try
        {
            // Auto Pilot
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.autoPilotEnabled ? green : red);
            ImGui.PushID(0);
            if (ImGui.TreeNodeEx("Auto Pilot", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.autoPilotEnabled.Value =
                    ImGuiExtension.Checkbox("Enabled", BetterFollowbotLite.Instance.Settings.autoPilotEnabled.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotGrace.Value =
                    ImGuiExtension.Checkbox("Remove Grace Period", BetterFollowbotLite.Instance.Settings.autoPilotGrace.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotLeader = ImGuiExtension.InputText("Leader Name: ", BetterFollowbotLite.Instance.Settings.autoPilotLeader, 60, ImGuiInputTextFlags.None);
                if (string.IsNullOrWhiteSpace(BetterFollowbotLite.Instance.Settings.autoPilotLeader.Value))
                {
                    // Show error message or set a default value
                    BetterFollowbotLite.Instance.Settings.autoPilotLeader.Value = "DefaultLeader";
                }
                else
                {
                    // Remove any invalid characters from the input
                    BetterFollowbotLite.Instance.Settings.autoPilotLeader.Value = new string(BetterFollowbotLite.Instance.Settings.autoPilotLeader.Value.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                }
                BetterFollowbotLite.Instance.Settings.autoPilotDashEnabled.Value = ImGuiExtension.Checkbox(
                    "Dash Enabled", BetterFollowbotLite.Instance.Settings.autoPilotDashEnabled.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotCloseFollow.Value = ImGuiExtension.Checkbox(
                    "Close Follow", BetterFollowbotLite.Instance.Settings.autoPilotCloseFollow.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotDashKey.Value = ImGuiExtension.HotkeySelector(
                    "Dash Key: " + BetterFollowbotLite.Instance.Settings.autoPilotDashKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotDashKey);
                BetterFollowbotLite.Instance.Settings.autoPilotMoveKey.Value = ImGuiExtension.HotkeySelector(
                    "Move Key: " + BetterFollowbotLite.Instance.Settings.autoPilotMoveKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotMoveKey);
                BetterFollowbotLite.Instance.Settings.autoPilotToggleKey.Value = ImGuiExtension.HotkeySelector(
                    "Toggle Key: " + BetterFollowbotLite.Instance.Settings.autoPilotToggleKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotToggleKey);
                /*
                BetterFollowbotLite.instance.Settings.autoPilotRandomClickOffset.Value =
                    ImGuiExtension.IntSlider("Random Click Offset", BetterFollowbotLite.instance.Settings.autoPilotRandomClickOffset);
                */
                BetterFollowbotLite.Instance.Settings.autoPilotInputFrequency.Value =
                    ImGuiExtension.IntSlider("Input Freq.", BetterFollowbotLite.Instance.Settings.autoPilotInputFrequency);
                BetterFollowbotLite.Instance.Settings.autoPilotPathfindingNodeDistance.Value =
                    ImGuiExtension.IntSlider("Keep within Distance", BetterFollowbotLite.Instance.Settings.autoPilotPathfindingNodeDistance);
                BetterFollowbotLite.Instance.Settings.autoPilotClearPathDistance.Value =
                    ImGuiExtension.IntSlider("Transition Distance", BetterFollowbotLite.Instance.Settings.autoPilotClearPathDistance);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }
            




        try
        {
            // Aura Blessing
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.auraBlessingEnabled ? green : red);
            ImGui.PushID(9);
            if (ImGui.TreeNodeEx("Aura Blessing", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.auraBlessingEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    BetterFollowbotLite.Instance.Settings.auraBlessingEnabled.Value);
                BetterFollowbotLite.Instance.Settings.auraBlessingWitheringStep.Value = 
                    ImGuiExtension.Checkbox("Do Not Override Withering Step", BetterFollowbotLite.Instance.Settings.auraBlessingWitheringStep.Value);
                BetterFollowbotLite.Instance.Settings.auraBlessingHpp.Value =
                    ImGuiExtension.IntSlider("HP%", BetterFollowbotLite.Instance.Settings.auraBlessingHpp);
                BetterFollowbotLite.Instance.Settings.auraBlessingEsp.Value =
                    ImGuiExtension.IntSlider("ES%", BetterFollowbotLite.Instance.Settings.auraBlessingEsp);
                BetterFollowbotLite.Instance.Settings.auraBlessingRange.Value =
                    ImGuiExtension.IntSlider("Range", BetterFollowbotLite.Instance.Settings.auraBlessingRange);
                BetterFollowbotLite.Instance.Settings.auraBlessingMinAny.Value =
                    ImGuiExtension.IntSlider("min Enemy Any", BetterFollowbotLite.Instance.Settings.auraBlessingMinAny);
                BetterFollowbotLite.Instance.Settings.auraBlessingMinRare.Value = ImGuiExtension.IntSlider("min Enemy Rare",
                    BetterFollowbotLite.Instance.Settings.auraBlessingMinRare);
                BetterFollowbotLite.Instance.Settings.auraBlessingMinUnique.Value = ImGuiExtension.IntSlider("min Enemy Unique",
                    BetterFollowbotLite.Instance.Settings.auraBlessingMinUnique);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            // Flame Link
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.flameLinkEnabled ? green : red);
            ImGui.PushID(28);
            if (ImGui.TreeNodeEx("Flame Link", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.flameLinkEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    BetterFollowbotLite.Instance.Settings.flameLinkEnabled.Value);
                BetterFollowbotLite.Instance.Settings.flameLinkRange.Value =
                    ImGuiExtension.IntSlider("Max Range to Leader", BetterFollowbotLite.Instance.Settings.flameLinkRange);
                BetterFollowbotLite.Instance.Settings.flameLinkTimeThreshold.Value =
                    ImGuiExtension.IntSlider("Recast when Timer < X seconds", BetterFollowbotLite.Instance.Settings.flameLinkTimeThreshold);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            ImGui.PushID(29);
            if (ImGui.TreeNodeEx("Smite Buff", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.smiteEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    BetterFollowbotLite.Instance.Settings.smiteEnabled.Value);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            ImGui.PushID(30);
            if (ImGui.TreeNodeEx("Vaal Skills", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.vaalHasteEnabled.Value = ImGuiExtension.Checkbox("Vaal Haste Enabled",
                    BetterFollowbotLite.Instance.Settings.vaalHasteEnabled.Value);
                BetterFollowbotLite.Instance.Settings.vaalDisciplineEnabled.Value = ImGuiExtension.Checkbox("Vaal Discipline Enabled",
                    BetterFollowbotLite.Instance.Settings.vaalDisciplineEnabled.Value);
                BetterFollowbotLite.Instance.Settings.vaalDisciplineEsp.Value =
                    ImGuiExtension.IntSlider("Vaal Discipline ES%", BetterFollowbotLite.Instance.Settings.vaalDisciplineEsp);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        //ImGui.End();
    }
}
