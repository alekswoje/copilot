using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace CoPilot;

internal class ImGuiDrawSettings
{
    private static Vector4 _donationColorTarget = new Vector4(0.454f, 0.031f, 0.768f, 1f);
    private static Vector4 _donationColorCurrent = new Vector4(0.454f, 0.031f, 0.768f, 1f);
    private static void SetText(string pText)
    {
        var staThread = new Thread(
            delegate()
            {
                // Use a fully qualified name for Clipboard otherwise it
                // will end up calling itself.
                Clipboard.SetText(pText);
            });
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
    }

    internal static void DrawImGuiSettings()
    {
        var green = new Vector4(0.102f, 0.388f, 0.106f, 1.000f);
        var red = new Vector4(0.388f, 0.102f, 0.102f, 1.000f);

        var collapsingHeaderFlags = ImGuiTreeNodeFlags.CollapsingHeader;
        ImGui.Text("Plugin by Totalschaden. https://github.com/totalschaden/copilot");

        try
        {
            // Input Keys
            ImGui.PushStyleColor(ImGuiCol.Header, green);
            ImGui.PushID(1000);
            if (ImGui.TreeNodeEx("Input Keys", collapsingHeaderFlags))
            {
                CoPilot.Instance.Settings.inputKey1.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 2 Key: " + CoPilot.Instance.Settings.inputKey1.Value,
                    CoPilot.Instance.Settings.inputKey1.Value);
                CoPilot.Instance.Settings.inputKey3.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 4 Key: " + CoPilot.Instance.Settings.inputKey3.Value,
                    CoPilot.Instance.Settings.inputKey3.Value);
                CoPilot.Instance.Settings.inputKey4.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 5 Key: " + CoPilot.Instance.Settings.inputKey4.Value,
                    CoPilot.Instance.Settings.inputKey4.Value);
                CoPilot.Instance.Settings.inputKey5.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 6 Key: " + CoPilot.Instance.Settings.inputKey5.Value,
                    CoPilot.Instance.Settings.inputKey5.Value);
                CoPilot.Instance.Settings.inputKey6.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 7 Key: " + CoPilot.Instance.Settings.inputKey6.Value,
                    CoPilot.Instance.Settings.inputKey6.Value);
                CoPilot.Instance.Settings.inputKey7.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 8 Key: " + CoPilot.Instance.Settings.inputKey7.Value,
                    CoPilot.Instance.Settings.inputKey7.Value);
                CoPilot.Instance.Settings.inputKey8.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 9 Key: " + CoPilot.Instance.Settings.inputKey8.Value,
                    CoPilot.Instance.Settings.inputKey8.Value);
                CoPilot.Instance.Settings.inputKey9.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 10 Key: " + CoPilot.Instance.Settings.inputKey9.Value,
                    CoPilot.Instance.Settings.inputKey9.Value);
                CoPilot.Instance.Settings.inputKey10.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 11 Key: " + CoPilot.Instance.Settings.inputKey10.Value,
                    CoPilot.Instance.Settings.inputKey10.Value);
                CoPilot.Instance.Settings.inputKey11.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 12 Key: " + CoPilot.Instance.Settings.inputKey11.Value,
                    CoPilot.Instance.Settings.inputKey11.Value);
                CoPilot.Instance.Settings.inputKey12.Value = ImGuiExtension.HotkeySelector(
                    "Use bound skill 13 Key: " + CoPilot.Instance.Settings.inputKey12.Value,
                    CoPilot.Instance.Settings.inputKey12.Value);
                CoPilot.Instance.Settings.inputKeyPickIt.Value = ImGuiExtension.HotkeySelector(
                    "PickIt Key: " + CoPilot.Instance.Settings.inputKeyPickIt.Value,
                    CoPilot.Instance.Settings.inputKeyPickIt.Value);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }

        try
        {
            ImGui.PushStyleColor(ImGuiCol.Header, CoPilot.Instance.Settings.confirm5 ? green : red);
            ImGui.PushID(1001);
            if (ImGui.TreeNodeEx("Important Information", collapsingHeaderFlags))
            {
                ImGui.Text(
                    "Go to Input Keys Tab, and set them According to your Ingame Settings -> Settings -> Input -> Use bound skill X");
                ImGui.NewLine();
                ImGui.Text(
                    "If your a noob dont make changes to -> Input Keys <- and change your Ingame Settings to the Keys that are predefined in the Plugin!");
                ImGui.NewLine();
                ImGui.Text("DO NOT ASSIGN MOUSE KEYS TO -> Input Keys <- in the Plugin !!!");
                ImGui.NewLine();
                ImGui.Text(
                    "The Top Left and Top Right Skill Slots are EXCLUDED!!! (Ingame bound skill 1 and 3) I recommend you use these for your Mouse.");
                ImGui.NewLine();
                ImGui.Text(
                    "The Plugin is currently forced to use Timers for Cooldowns as there is no Proper Skill Api for Ready/Cooldown.");
                ImGui.NewLine();
                ImGui.Text(
                    "I STRONGLY recommend that you add 80-100ms extra delay to your Skill Settings, so a Skill wont be skipped sometimes.");
                ImGui.NewLine();
                ImGui.Text("Unhappy with Cooldown Slider ? Set your own Value with STRG/CTRL + Mouseclick");
                ImGui.NewLine();
                ImGui.Text(
                    "Using Auto Attack and Cyclone for example? If you want to Cyclone yourself, put it on Right Mouse Slot, and have Cyclone in the Slot before that one.");
                ImGui.NewLine();


                CoPilot.Instance.Settings.confirm1.Value = ImGuiExtension.Checkbox("I did READ the text above.",
                    CoPilot.Instance.Settings.confirm1.Value);
                if (!CoPilot.Instance.Settings.confirm1)
                    return;
                CoPilot.Instance.Settings.confirm2.Value = ImGuiExtension.Checkbox(
                    "I did READ and UNDERSTAND the text above.", CoPilot.Instance.Settings.confirm2.Value);
                if (!CoPilot.Instance.Settings.confirm2)
                    return;
                CoPilot.Instance.Settings.confirm3.Value = ImGuiExtension.Checkbox(
                    "I just READ it again and understood it.", CoPilot.Instance.Settings.confirm3.Value);
                if (!CoPilot.Instance.Settings.confirm3)
                    return;
                CoPilot.Instance.Settings.confirm4.Value = ImGuiExtension.Checkbox(
                    "I did everything stated above and im ready to go.", CoPilot.Instance.Settings.confirm4.Value);
                if (!CoPilot.Instance.Settings.confirm4)
                    return;
                CoPilot.Instance.Settings.confirm5.Value =
                    ImGuiExtension.Checkbox("Let me use the Plugin already !!!",
                        CoPilot.Instance.Settings.confirm5.Value);
                if (!CoPilot.Instance.Settings.confirm5)
                    return;
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }

        if (!CoPilot.Instance.Settings.confirm5)
            return;

        try
        {
            // Donation
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (_donationColorCurrent.X == _donationColorTarget.X &&
                _donationColorCurrent.Y == _donationColorTarget.Y &&
                _donationColorCurrent.Z == _donationColorTarget.Z)
                // ReSharper restore CompareOfFloatsByEqualityOperator
            {
                _donationColorTarget = new Vector4(Helper.random.NextFloat(0, 1), Helper.random.NextFloat(0, 1),
                    Helper.random.NextFloat(0, 1), 1f);
            }
            else
            {
                var deltaTime = SkillInfo._deltaTime / 1000;
                    
                _donationColorCurrent.X = Helper.MoveTowards(_donationColorCurrent.X, _donationColorTarget.X, deltaTime);
                _donationColorCurrent.Y = Helper.MoveTowards(_donationColorCurrent.Y, _donationColorTarget.Y, deltaTime);
                _donationColorCurrent.Z = Helper.MoveTowards(_donationColorCurrent.Z, _donationColorTarget.Z, deltaTime);
            }
            ImGui.PushStyleColor(ImGuiCol.Header, _donationColorCurrent);
            ImGui.PushID(99999);
            if (ImGui.TreeNodeEx("Donation - Send some Snacks nom nom", collapsingHeaderFlags))
            {
                ImGui.Text("Thanks to anyone who is considering this.");
                if (ImGui.Button("Copy Amazon.de Wishlist URL")) SetText("https://www.amazon.de/hz/wishlist/ls/MZ543BDBC6PJ?ref_=wl_share");
                if (ImGui.Button("Copy BTC Adress")) SetText("bc1qwjpdf9q3n94e88m3z398udjagach5u56txwpkh");
                if (ImGui.Button("Copy ETH Adress")) SetText("0x78Af12D08B32f816dB9788C5Cf3122693143ed78");
                if (ImGui.Button("Copy LTC Adress")) SetText("LXCoWiLS5ZKEzHb7yTpJ7AxJrU9QLhCyHR");
                CoPilot.Instance.Settings.debugMode.Value = ImGuiExtension.Checkbox("Turn on Debug Mode",
                    CoPilot.Instance.Settings.debugMode.Value);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }

        try
        {
            // Auto Pilot
            ImGui.PushStyleColor(ImGuiCol.Header, CoPilot.Instance.Settings.autoPilotEnabled ? green : red);
            ImGui.PushID(0);
            if (ImGui.TreeNodeEx("Auto Pilot", collapsingHeaderFlags))
            {
                CoPilot.Instance.Settings.autoPilotEnabled.Value =
                    ImGuiExtension.Checkbox("Enabled", CoPilot.Instance.Settings.autoPilotEnabled.Value);
                CoPilot.Instance.Settings.autoPilotGrace.Value =
                    ImGuiExtension.Checkbox("Remove Grace Period", CoPilot.Instance.Settings.autoPilotGrace.Value);
                CoPilot.Instance.Settings.autoPilotLeader = ImGuiExtension.InputText("Leader Name: ", CoPilot.Instance.Settings.autoPilotLeader, 60, ImGuiInputTextFlags.None);
                if (string.IsNullOrWhiteSpace(CoPilot.Instance.Settings.autoPilotLeader.Value))
                {
                    // Show error message or set a default value
                    CoPilot.Instance.Settings.autoPilotLeader.Value = "DefaultLeader";
                }
                else
                {
                    // Remove any invalid characters from the input
                    CoPilot.Instance.Settings.autoPilotLeader.Value = new string(CoPilot.Instance.Settings.autoPilotLeader.Value.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
                }
                CoPilot.Instance.Settings.autoPilotDashEnabled.Value = ImGuiExtension.Checkbox(
                    "Dash Enabled", CoPilot.Instance.Settings.autoPilotDashEnabled.Value);
                CoPilot.Instance.Settings.autoPilotCloseFollow.Value = ImGuiExtension.Checkbox(
                    "Close Follow", CoPilot.Instance.Settings.autoPilotCloseFollow.Value);
                CoPilot.Instance.Settings.autoPilotDashKey.Value = ImGuiExtension.HotkeySelector(
                    "Dash Key: " + CoPilot.Instance.Settings.autoPilotDashKey.Value, CoPilot.Instance.Settings.autoPilotDashKey);
                CoPilot.Instance.Settings.autoPilotMoveKey.Value = ImGuiExtension.HotkeySelector(
                    "Move Key: " + CoPilot.Instance.Settings.autoPilotMoveKey.Value, CoPilot.Instance.Settings.autoPilotMoveKey);
                CoPilot.Instance.Settings.autoPilotToggleKey.Value = ImGuiExtension.HotkeySelector(
                    "Toggle Key: " + CoPilot.Instance.Settings.autoPilotToggleKey.Value, CoPilot.Instance.Settings.autoPilotToggleKey);
                CoPilot.Instance.Settings.autoPilotTakeWaypoints.Value = ImGuiExtension.Checkbox(
                    "Take Waypoints", CoPilot.Instance.Settings.autoPilotTakeWaypoints.Value);
                /*
                CoPilot.instance.Settings.autoPilotRandomClickOffset.Value =
                    ImGuiExtension.IntSlider("Random Click Offset", CoPilot.instance.Settings.autoPilotRandomClickOffset);
                */
                CoPilot.Instance.Settings.autoPilotInputFrequency.Value =
                    ImGuiExtension.IntSlider("Input Freq.", CoPilot.Instance.Settings.autoPilotInputFrequency);
                CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value =
                    ImGuiExtension.IntSlider("Keep within Distance", CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance);
                CoPilot.Instance.Settings.autoPilotClearPathDistance.Value =
                    ImGuiExtension.IntSlider("Transition Distance", CoPilot.Instance.Settings.autoPilotClearPathDistance);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }
            




        try
        {
            // Auto Quit
            ImGui.PushStyleColor(ImGuiCol.Header, CoPilot.Instance.Settings.autoQuitEnabled ? green : red);
            ImGui.PushID(3);
            if (ImGui.TreeNodeEx("Auto Quit (This requires HUD started as Admin !)", collapsingHeaderFlags))
            {
                CoPilot.Instance.Settings.autoQuitEnabled.Value =
                    ImGuiExtension.Checkbox("Enabled", CoPilot.Instance.Settings.autoQuitEnabled.Value);
                CoPilot.Instance.Settings.hppQuit.Value =
                    ImGuiExtension.IntSlider("HP%", CoPilot.Instance.Settings.hppQuit);
                CoPilot.Instance.Settings.espQuit.Value =
                    ImGuiExtension.IntSlider("ES%", CoPilot.Instance.Settings.espQuit);
                CoPilot.Instance.Settings.autoQuitGuardian.Value = ImGuiExtension.Checkbox("Guardian Auto Quit",
                    CoPilot.Instance.Settings.autoQuitGuardian.Value);
                CoPilot.Instance.Settings.guardianHpp.Value =
                    ImGuiExtension.IntSlider("Guardian HP%", CoPilot.Instance.Settings.guardianHpp);
                CoPilot.Instance.Settings.autoQuitHotkeyEnabled.Value = ImGuiExtension.Checkbox("Hotkey Enabled",
                    CoPilot.Instance.Settings.autoQuitHotkeyEnabled.Value);
                CoPilot.Instance.Settings.forcedAutoQuit.Value = ImGuiExtension.HotkeySelector(
                    "Force Quit Hotkey: " + CoPilot.Instance.Settings.forcedAutoQuit.Value,
                    CoPilot.Instance.Settings.forcedAutoQuit.Value);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }




            
            
        try
        {
            // Aura Blessing
            ImGui.PushStyleColor(ImGuiCol.Header, CoPilot.Instance.Settings.auraBlessingEnabled ? green : red);
            ImGui.PushID(9);
            if (ImGui.TreeNodeEx("Aura Blessing", collapsingHeaderFlags))
            {
                CoPilot.Instance.Settings.auraBlessingEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    CoPilot.Instance.Settings.auraBlessingEnabled.Value);
                CoPilot.Instance.Settings.auraBlessingWitheringStep.Value = 
                    ImGuiExtension.Checkbox("Do Not Override Withering Step", CoPilot.Instance.Settings.auraBlessingWitheringStep.Value);
                CoPilot.Instance.Settings.auraBlessingHpp.Value =
                    ImGuiExtension.IntSlider("HP%", CoPilot.Instance.Settings.auraBlessingHpp);
                CoPilot.Instance.Settings.auraBlessingEsp.Value =
                    ImGuiExtension.IntSlider("ES%", CoPilot.Instance.Settings.auraBlessingEsp);
                CoPilot.Instance.Settings.auraBlessingRange.Value =
                    ImGuiExtension.IntSlider("Range", CoPilot.Instance.Settings.auraBlessingRange);
                CoPilot.Instance.Settings.auraBlessingMinAny.Value =
                    ImGuiExtension.IntSlider("min Enemy Any", CoPilot.Instance.Settings.auraBlessingMinAny);
                CoPilot.Instance.Settings.auraBlessingMinRare.Value = ImGuiExtension.IntSlider("min Enemy Rare",
                    CoPilot.Instance.Settings.auraBlessingMinRare);
                CoPilot.Instance.Settings.auraBlessingMinUnique.Value = ImGuiExtension.IntSlider("min Enemy Unique",
                    CoPilot.Instance.Settings.auraBlessingMinUnique);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }

        try
        {
            // Flame Link
            ImGui.PushStyleColor(ImGuiCol.Header, CoPilot.Instance.Settings.flameLinkEnabled ? green : red);
            ImGui.PushID(28);
            if (ImGui.TreeNodeEx("Flame Link", collapsingHeaderFlags))
            {
                CoPilot.Instance.Settings.flameLinkEnabled.Value = ImGuiExtension.Checkbox("Enabled",
                    CoPilot.Instance.Settings.flameLinkEnabled.Value);
                CoPilot.Instance.Settings.flameLinkRange.Value =
                    ImGuiExtension.IntSlider("Max Range to Leader", CoPilot.Instance.Settings.flameLinkRange);
                CoPilot.Instance.Settings.flameLinkTimeThreshold.Value =
                    ImGuiExtension.IntSlider("Recast when Timer < X seconds", CoPilot.Instance.Settings.flameLinkTimeThreshold);
            }
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError(e.ToString());
        }




        //ImGui.End();
    }
}
