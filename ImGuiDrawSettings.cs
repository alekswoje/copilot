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
                    "Skill 2: " + BetterFollowbotLite.Instance.Settings.inputKey1.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey1.Value);
                BetterFollowbotLite.Instance.Settings.inputKey3.Value = ImGuiExtension.HotkeySelector(
                    "Skill 4: " + BetterFollowbotLite.Instance.Settings.inputKey3.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey3.Value);
                BetterFollowbotLite.Instance.Settings.inputKey4.Value = ImGuiExtension.HotkeySelector(
                    "Skill 5: " + BetterFollowbotLite.Instance.Settings.inputKey4.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey4.Value);
                BetterFollowbotLite.Instance.Settings.inputKey5.Value = ImGuiExtension.HotkeySelector(
                    "Skill 6: " + BetterFollowbotLite.Instance.Settings.inputKey5.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey5.Value);
                BetterFollowbotLite.Instance.Settings.inputKey6.Value = ImGuiExtension.HotkeySelector(
                    "Skill 7: " + BetterFollowbotLite.Instance.Settings.inputKey6.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey6.Value);
                BetterFollowbotLite.Instance.Settings.inputKey7.Value = ImGuiExtension.HotkeySelector(
                    "Skill 8: " + BetterFollowbotLite.Instance.Settings.inputKey7.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey7.Value);
                BetterFollowbotLite.Instance.Settings.inputKey8.Value = ImGuiExtension.HotkeySelector(
                    "Skill 9: " + BetterFollowbotLite.Instance.Settings.inputKey8.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey8.Value);
                BetterFollowbotLite.Instance.Settings.inputKey9.Value = ImGuiExtension.HotkeySelector(
                    "Skill 10: " + BetterFollowbotLite.Instance.Settings.inputKey9.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey9.Value);
                BetterFollowbotLite.Instance.Settings.inputKey10.Value = ImGuiExtension.HotkeySelector(
                    "Skill 11: " + BetterFollowbotLite.Instance.Settings.inputKey10.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey10.Value);
                BetterFollowbotLite.Instance.Settings.inputKey11.Value = ImGuiExtension.HotkeySelector(
                    "Skill 12: " + BetterFollowbotLite.Instance.Settings.inputKey11.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey11.Value);
                BetterFollowbotLite.Instance.Settings.inputKey12.Value = ImGuiExtension.HotkeySelector(
                    "Skill 13: " + BetterFollowbotLite.Instance.Settings.inputKey12.Value,
                    BetterFollowbotLite.Instance.Settings.inputKey12.Value);
                BetterFollowbotLite.Instance.Settings.inputKeyPickIt.Value = ImGuiExtension.HotkeySelector(
                    "PickIt: " + BetterFollowbotLite.Instance.Settings.inputKeyPickIt.Value,
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
                    ImGuiExtension.Checkbox("No Grace Period", BetterFollowbotLite.Instance.Settings.autoPilotGrace.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotLeader = ImGuiExtension.InputText("Leader: ", BetterFollowbotLite.Instance.Settings.autoPilotLeader, 60, ImGuiInputTextFlags.None);
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
                    "Dash", BetterFollowbotLite.Instance.Settings.autoPilotDashEnabled.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotCloseFollow.Value = ImGuiExtension.Checkbox(
                    "Close Follow", BetterFollowbotLite.Instance.Settings.autoPilotCloseFollow.Value);
                BetterFollowbotLite.Instance.Settings.autoPilotDashKey.Value = ImGuiExtension.HotkeySelector(
                    "Dash Key: " + BetterFollowbotLite.Instance.Settings.autoPilotDashKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotDashKey);
                BetterFollowbotLite.Instance.Settings.autoPilotMoveKey.Value = ImGuiExtension.HotkeySelector(
                    "Move Key: " + BetterFollowbotLite.Instance.Settings.autoPilotMoveKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotMoveKey);
                BetterFollowbotLite.Instance.Settings.autoPilotToggleKey.Value = ImGuiExtension.HotkeySelector(
                    "Toggle: " + BetterFollowbotLite.Instance.Settings.autoPilotToggleKey.Value, BetterFollowbotLite.Instance.Settings.autoPilotToggleKey);
                /*
                BetterFollowbotLite.instance.Settings.autoPilotRandomClickOffset.Value =
                    ImGuiExtension.IntSlider("Random Click Offset", BetterFollowbotLite.instance.Settings.autoPilotRandomClickOffset);
                */
                BetterFollowbotLite.Instance.Settings.autoPilotInputFrequency.Value =
                    ImGuiExtension.IntSlider("Input Freq", BetterFollowbotLite.Instance.Settings.autoPilotInputFrequency);
                BetterFollowbotLite.Instance.Settings.autoPilotPathfindingNodeDistance.Value =
                    ImGuiExtension.IntSlider("Follow Distance", BetterFollowbotLite.Instance.Settings.autoPilotPathfindingNodeDistance);
                BetterFollowbotLite.Instance.Settings.autoPilotClearPathDistance.Value =
                    ImGuiExtension.IntSlider("Transition Dist", BetterFollowbotLite.Instance.Settings.autoPilotClearPathDistance);
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
                BetterFollowbotLite.Instance.Settings.holyRelicHealthThreshold.Value =
                    ImGuiExtension.IntSlider("Holy Relic Health %", BetterFollowbotLite.Instance.Settings.holyRelicHealthThreshold);
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
                    ImGuiExtension.IntSlider("Range", BetterFollowbotLite.Instance.Settings.flameLinkRange);
                BetterFollowbotLite.Instance.Settings.flameLinkTimeThreshold.Value =
                    ImGuiExtension.IntSlider("Recast Timer", BetterFollowbotLite.Instance.Settings.flameLinkTimeThreshold);
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

        try
        {
            // Mines
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.minesEnabled ? green : red);
            ImGui.PushID(31);
            if (ImGui.TreeNodeEx("Mines", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.minesEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    BetterFollowbotLite.Instance.Settings.minesEnabled.Value);
                BetterFollowbotLite.Instance.Settings.minesRange = ImGuiExtension.InputText("Range",
                    BetterFollowbotLite.Instance.Settings.minesRange, 60, ImGuiInputTextFlags.None);
                BetterFollowbotLite.Instance.Settings.minesLeaderDistance = ImGuiExtension.InputText("Leader Distance",
                    BetterFollowbotLite.Instance.Settings.minesLeaderDistance, 60, ImGuiInputTextFlags.None);
                BetterFollowbotLite.Instance.Settings.minesStormblastEnabled.Value = ImGuiExtension.Checkbox("Stormblast",
                    BetterFollowbotLite.Instance.Settings.minesStormblastEnabled.Value);
                BetterFollowbotLite.Instance.Settings.minesPyroclastEnabled.Value = ImGuiExtension.Checkbox("Pyroclast",
                    BetterFollowbotLite.Instance.Settings.minesPyroclastEnabled.Value);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            // Auto Respawn
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.autoRespawnEnabled ? green : red);
            ImGui.PushID(32);
            if (ImGui.TreeNodeEx("Auto Respawn", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.autoRespawnEnabled.Value = ImGuiExtension.Checkbox("Auto Respawn",
                    BetterFollowbotLite.Instance.Settings.autoRespawnEnabled.Value);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            // Summon Skeletons
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.summonSkeletonsEnabled ? green : red);
            ImGui.PushID(33);
            if (ImGui.TreeNodeEx("Summon Skeletons", collapsingHeaderFlags))
            {
                BetterFollowbotLite.Instance.Settings.summonSkeletonsEnabled.Value = ImGuiExtension.Checkbox("Auto Summon Skeletons",
                    BetterFollowbotLite.Instance.Settings.summonSkeletonsEnabled.Value);

                BetterFollowbotLite.Instance.Settings.summonSkeletonsRange.Value =
                    ImGuiExtension.IntSlider("Range", BetterFollowbotLite.Instance.Settings.summonSkeletonsRange);

                BetterFollowbotLite.Instance.Settings.summonSkeletonsMinCount.Value =
                    ImGuiExtension.IntSlider("Min Count", BetterFollowbotLite.Instance.Settings.summonSkeletonsMinCount);
            }
        }
        catch (Exception e)
        {
            // Error handling without logging
        }

        try
        {
            // Auto Level Gems
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled ? green : red);
            ImGui.PushID(34);
            if (ImGui.TreeNodeEx("Auto Level Gems", collapsingHeaderFlags))
            {
                var currentValue = BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value;
                var newValue = ImGuiExtension.Checkbox("Auto Level Gems", currentValue);

                if (newValue != currentValue)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Checkbox changed from {currentValue} to {newValue}");
                    BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value = newValue;
                }

                // Debug: Show current value
                ImGui.Text($"Current: {BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value}");

                if (ImGui.Button("Test"))
                {
                    var oldValue = BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value;
                    BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value = !oldValue;
                    BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Test toggled from {oldValue} to {BetterFollowbotLite.Instance.Settings.autoLevelGemsEnabled.Value}");
                }
            }
        }
        catch (Exception e)
        {
            BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS UI ERROR: {e.Message}");
        }

        try
        {
            // Auto Join Party
            ImGui.PushStyleColor(ImGuiCol.Header, BetterFollowbotLite.Instance.Settings.autoJoinPartyEnabled ? green : red);
            ImGui.PushID(35);
            if (ImGui.TreeNodeEx("Auto Join Party", collapsingHeaderFlags))
            {
                var currentValue = BetterFollowbotLite.Instance.Settings.autoJoinPartyEnabled.Value;
                var newValue = ImGuiExtension.Checkbox("Auto Join Party Invites", currentValue);

                if (newValue != currentValue)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Checkbox changed from {currentValue} to {newValue}");
                    BetterFollowbotLite.Instance.Settings.autoJoinPartyEnabled.Value = newValue;
                }

                // Debug: Show current value
                ImGui.Text($"Current: {BetterFollowbotLite.Instance.Settings.autoJoinPartyEnabled.Value}");
            }
        }
        catch (Exception e)
        {
            BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY UI ERROR: {e.Message}");
        }

        //ImGui.End();
    }
}
