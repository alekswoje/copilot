using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
// ReSharper disable FieldCanBeMadeReadOnly.Global
// Need non readonly to save settings.

namespace BetterFollowbotLite;

public class BetterFollowbotLiteSettings : ISettings
{
    #region Auto Map Tabber

    public ToggleNode autoMapTabber = new ToggleNode(false);

    #endregion

    public ToggleNode debugMode = new ToggleNode(false);

    public BetterFollowbotLiteSettings()
    {
        Enable = new ToggleNode(false);
    }

    public ToggleNode Enable { get; set; }

    #region AutoPilot
        
    public ToggleNode autoPilotEnabled = new ToggleNode(false);
    public ToggleNode autoPilotGrace = new ToggleNode(true);
    public TextNode autoPilotLeader = new TextNode("");
    public ToggleNode autoPilotDashEnabled = new ToggleNode(false);
    public ToggleNode autoPilotCloseFollow = new ToggleNode(true);
    public HotkeyNode autoPilotDashKey = new HotkeyNode(Keys.W);
    public HotkeyNode autoPilotMoveKey = new HotkeyNode(Keys.Q);
    public HotkeyNode autoPilotToggleKey = new HotkeyNode(Keys.NumPad9);
    public RangeNode<int> autoPilotRandomClickOffset = new RangeNode<int>(10, 1, 100);
    public RangeNode<int> autoPilotInputFrequency = new RangeNode<int>(50, 1, 100);
    public RangeNode<int> autoPilotPathfindingNodeDistance = new RangeNode<int>(200, 10, 1000);
    public RangeNode<int> autoPilotClearPathDistance = new RangeNode<int>(500, 100, 5000);

    #endregion
        

    #region Aura Blessing

    public ToggleNode auraBlessingEnabled = new ToggleNode(false);
    public ToggleNode auraBlessingWitheringStep = new ToggleNode(false);
    public RangeNode<int> auraBlessingRange = new RangeNode<int>(550, 100, 1000);
    public RangeNode<int> auraBlessingHpp = new RangeNode<int>(100, 0, 100);
    public RangeNode<int> auraBlessingEsp = new RangeNode<int>(0, 0, 100);
    public RangeNode<int> auraBlessingMinAny = new RangeNode<int>(1, 0, 50);
    public RangeNode<int> auraBlessingMinRare = new RangeNode<int>(0, 0, 50);
    public RangeNode<int> auraBlessingMinUnique = new RangeNode<int>(0, 0, 50);
    public TextNode auraBlessingName = new TextNode("");
    public TextNode auraBlessing = new TextNode("");

    #endregion

    #region Link Skills

    public ToggleNode flameLinkEnabled = new ToggleNode(false);
    public RangeNode<int> flameLinkRange = new RangeNode<int>(40, 10, 100);
    public RangeNode<int> flameLinkTimeThreshold = new RangeNode<int>(4, 1, 10);

    #endregion

    #region Smite Buff

    public ToggleNode smiteEnabled = new ToggleNode(false);

    #endregion

    #region Input Keys

    public HotkeyNode inputKey1 = new HotkeyNode(Keys.Z);
    public HotkeyNode inputKey3 = new HotkeyNode(Keys.Q);
    public HotkeyNode inputKey4 = new HotkeyNode(Keys.W);
    public HotkeyNode inputKey5 = new HotkeyNode(Keys.E);
    public HotkeyNode inputKey6 = new HotkeyNode(Keys.R);
    public HotkeyNode inputKey7 = new HotkeyNode(Keys.T);
    public HotkeyNode inputKey8 = new HotkeyNode(Keys.NumPad1);
    public HotkeyNode inputKey9 = new HotkeyNode(Keys.NumPad2);
    public HotkeyNode inputKey10 = new HotkeyNode(Keys.NumPad3);
    public HotkeyNode inputKey11 = new HotkeyNode(Keys.NumPad4);
    public HotkeyNode inputKey12 = new HotkeyNode(Keys.NumPad5);
    public HotkeyNode inputKeyPickIt = new HotkeyNode(Keys.Space);

    #endregion

}