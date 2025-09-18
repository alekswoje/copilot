using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace BetterFollowbotLite;

public class BetterFollowbotLite : BaseSettingsPlugin<BetterFollowbotLiteSettings>
{
    private const int Delay = 45;

    private const int MouseAutoSnapRange = 250;
    internal static BetterFollowbotLite Instance;
    internal AutoPilot autoPilot = new AutoPilot();
    private readonly Summons summons = new Summons();

    private List<Buff> buffs;
    private List<Entity> enemys = new List<Entity>();
    private bool isAttacking;
    private bool isCasting;
    private bool isMoving;
    internal DateTime lastTimeAny;
    private DateTime lastAutoJoinPartyAttempt;
    private DateTime lastAreaChangeTime = DateTime.MinValue;
    internal Entity localPlayer;
    internal Life player;
    internal Vector3 playerPosition;
    private Coroutine skillCoroutine;
    internal List<ActorSkill> skills = new List<ActorSkill>();
    private List<ActorVaalSkill> vaalSkills = new List<ActorVaalSkill>();



    public override bool Initialise()
    {
        if (Instance == null)
            Instance = this;

        // Initialize timestamps
        lastAutoJoinPartyAttempt = DateTime.Now;

        GameController.LeftPanel.WantUse(() => Settings.Enable);
        skillCoroutine = new Coroutine(WaitForSkillsAfterAreaChange(), this);
        Core.ParallelRunner.Run(skillCoroutine);
        Input.RegisterKey(Settings.autoPilotToggleKey.Value);
        Settings.autoPilotToggleKey.OnValueChanged += () => { Input.RegisterKey(Settings.autoPilotToggleKey.Value); };
        autoPilot.StartCoroutine();
        return true;
    }
        

    private int GetMinnionsWithin(float maxDistance)
    {
        return localPlayer.GetComponent<Actor>().DeployedObjects.Where(x => x?.Entity != null && x.Entity.IsAlive).Select(minnion => Vector2.Distance(new Vector2(minnion.Entity.Pos.X, minnion.Entity.Pos.Y), new Vector2(playerPosition.X, playerPosition.Y))).Count(distance => distance <= maxDistance);
    }

    private int GetMonsterWithin(float maxDistance, MonsterRarity rarity = MonsterRarity.White)
    {
        return (from monster in enemys where monster.Rarity >= rarity select Vector2.Distance(new Vector2(monster.PosNum.X, monster.PosNum.Y), new Vector2(playerPosition.X, playerPosition.Y))).Count(distance => distance <= maxDistance);
    }
        

    private bool MonsterCheck(int range, int minAny, int minRare, int minUnique)
    {
        int any = 0, rare = 0, unique = 0;
        foreach (var monster in enemys)
            switch (monster.Rarity)
            {
                case MonsterRarity.White:
                {
                    if (Vector2.Distance(new Vector2(monster.PosNum.X, monster.PosNum.Y),
                            new Vector2(playerPosition.X, playerPosition.Y)) <= range)
                        any++;
                    break;
                }
                case MonsterRarity.Magic:
                {
                    if (Vector2.Distance(new Vector2(monster.PosNum.X, monster.PosNum.Y),
                            new Vector2(playerPosition.X, playerPosition.Y)) <= range)
                        any++;
                    break;
                }
                case MonsterRarity.Rare:
                {
                    if (Vector2.Distance(new Vector2(monster.PosNum.X, monster.PosNum.Y),
                            new Vector2(playerPosition.X, playerPosition.Y)) <= range)
                    {
                        any++;
                        rare++;
                    }
                    break;
                }
                case MonsterRarity.Unique:
                {
                    if (Vector2.Distance(new Vector2(monster.PosNum.X, monster.PosNum.Y),
                            new Vector2(playerPosition.X, playerPosition.Y)) <= range)
                    {
                        any++;
                        rare++;
                        unique++;
                    }
                    break;
                }
            }

        if (minUnique > 0 && unique >= minUnique) return true;

        if (minRare > 0 && rare >= minRare) return true;

        if (minAny > 0 && any >= minAny) return true;

        return minAny == 0 && minRare == 0 && minUnique == 0;
    }

    internal Vector2 GetMousePosition()
    {
        return new Vector2(GameController.IngameState.MousePosX, GameController.IngameState.MousePosY);
    }


    public bool Gcd()
    {
        return (DateTime.Now - lastTimeAny).TotalMilliseconds > Delay;
    }

    private void Quit()
    {
        try
        {
            CommandHandler.KillTcpConnectionForProcess(GameController.Window.Process.Id);
        }
        catch (Exception e)
        {
            // Error handling without logging
        }
    }

    private Keys GetSkillInputKey(int index)
    {
        return index switch
        {
            1 => Settings.inputKey1.Value,
            3 => Settings.inputKey3.Value,
            4 => Settings.inputKey4.Value,
            5 => Settings.inputKey5.Value,
            6 => Settings.inputKey6.Value,
            7 => Settings.inputKey7.Value,
            8 => Settings.inputKey8.Value,
            9 => Settings.inputKey9.Value,
            10 => Settings.inputKey10.Value,
            11 => Settings.inputKey11.Value,
            12 => Settings.inputKey12.Value,
            _ => Keys.Escape
        };
    }

    private IEnumerator WaitForSkillsAfterAreaChange()
    {
        while (skills == null || localPlayer == null || GameController.IsLoading || !GameController.InGame)
            yield return new WaitTime(200);

        yield return new WaitTime(1000);
        SkillInfo.UpdateSkillInfo(true);
    }

    public override void AreaChange(AreaInstance area)
    {
        base.AreaChange(area);

        // Track area change time to prevent random movement during transitions
        lastAreaChangeTime = DateTime.Now;

        // Log area change details
        var newAreaName = area?.DisplayName ?? "Unknown";
        var isHideout = area?.IsHideout ?? false;
        var realLevel = area?.RealLevel ?? 0;

        LogMessage($"AREA CHANGE: Transitioned to '{newAreaName}' - Hideout: {isHideout}, Level: {realLevel}");

        SkillInfo.ResetSkills();
        skills = null;

        var coroutine = new Coroutine(WaitForSkillsAfterAreaChange(), this);
        Core.ParallelRunner.Run(coroutine);

        autoPilot.AreaChange();

        LogMessage("AREA CHANGE: Area change processing completed");
    }
        
    public override void DrawSettings()
    {
        //base.DrawSettings();

        // Draw Custom GUI
        if (Settings.Enable)
            ImGuiDrawSettings.DrawImGuiSettings();
    }

    private static bool HasStat(Entity monster, GameStat stat)
    {
        // Using this with GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster].Where
        // seems to cause Nullref errors on TC Fork. Where using the Code directly in a check seems fine, must have to do with Entity Parameter.
        // Maybe someone knows why, i dont :)
        try
        {
            var value = monster?.GetComponent<Stats>()?.StatDictionary?[stat];
            return value > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public override void Render()
    {
        try
        {
            if (!Settings.Enable) return;
            SkillInfo.GetDeltaTime();
                
            try
            {
                // Grace period removal with safeguards to prevent random movement during zone transitions
                if (Settings.autoPilotEnabled && Settings.autoPilotGrace && buffs != null && buffs.Exists(x => x.Name == "grace_period") && Gcd())
                {
                    // Prevent random movement during zone transitions (first 3 seconds after area change)
                    var timeSinceAreaChange = (DateTime.Now - lastAreaChangeTime).TotalSeconds;
                    if (timeSinceAreaChange > 3.0)
                    {
                        // Simple check: only press move key if player appears to be stationary
                        // This prevents interfering with existing movement during zone transitions
                        var isMoving = false;

                        if (localPlayer != null)
                        {
                            var positionComponent = localPlayer.GetComponent<Positioned>();
                            if (positionComponent != null)
                            {
                                var currentPos = positionComponent.GridPosition;
                                var distanceMoved = Vector3.Distance(currentPos, playerPosition);

                                // If moved more than 10 units since last check, consider player moving
                                isMoving = distanceMoved > 10.0f;

                                // Update stored position for next check
                                playerPosition = currentPos;
                            }
                        }

                        if (!isMoving)
                        {
                            LogMessage("GRACE PERIOD: Removing grace period buff (player appears stationary)");
                            Keyboard.KeyPress(Settings.autoPilotMoveKey);
                        }
                        else
                        {
                            LogMessage("GRACE PERIOD: Skipping grace removal - player appears to be moving");
                        }
                    }
                    else
                    {
                        LogMessage($"GRACE PERIOD: Skipping grace removal during zone transition ({timeSinceAreaChange:F1}s since area change)");
                    }
                }
                autoPilot.UpdateAutoPilotLogic();
                autoPilot.Render();
            }
            catch (Exception e)
            {
                // Error handling without logging
            }


            if (GameController?.Game?.IngameState?.Data?.LocalPlayer == null || GameController?.IngameState?.IngameUi == null )
                return;
            var chatField = GameController?.IngameState?.IngameUi?.ChatPanel?.ChatInputElement?.IsVisible;
            if (chatField != null && (bool)chatField)
                return;
                    
            localPlayer = GameController.Game.IngameState.Data.LocalPlayer;
            player = localPlayer.GetComponent<Life>();
            buffs = localPlayer.GetComponent<Buffs>().BuffsList;
            isAttacking = localPlayer.GetComponent<Actor>().isAttacking;
            isCasting = localPlayer.GetComponent<Actor>().Action.HasFlag(ActionFlags.UsingAbility);
            isMoving = localPlayer.GetComponent<Actor>().isMoving;
            skills = localPlayer.GetComponent<Actor>().ActorSkills;
            vaalSkills = localPlayer.GetComponent<Actor>().ActorVaalSkills;
            playerPosition = localPlayer.Pos;

            #region Auto Respawn

            try
            {
                if (Settings.autoRespawnEnabled && Gcd())
                {
                    // Check if the resurrect panel is visible
                    var resurrectPanel = GameController.IngameState.IngameUi.ResurrectPanel;
                    if (resurrectPanel != null && resurrectPanel.IsVisible)
                    {
                        // Check if the resurrect at checkpoint button is available
                        var resurrectAtCheckpoint = resurrectPanel.ResurrectAtCheckpoint;
                        if (resurrectAtCheckpoint != null && resurrectAtCheckpoint.IsVisible)
                        {
                            BetterFollowbotLite.Instance.LogMessage("AUTO RESPAWN: Respawn panel detected, attempting checkpoint respawn");

                            // Get the center position of the checkpoint respawn button
                            var checkpointRect = resurrectAtCheckpoint.GetClientRectCache;
                            var checkpointCenter = checkpointRect.Center;

                            BetterFollowbotLite.Instance.LogMessage($"AUTO RESPAWN: Checkpoint button position - X: {checkpointCenter.X:F1}, Y: {checkpointCenter.Y:F1}");

                            // Move mouse to the checkpoint respawn button with proper timing
                            Mouse.SetCursorPos(checkpointCenter);

                            // Wait longer to ensure mouse movement is registered and UI is ready
                            System.Threading.Thread.Sleep(200);

                            // Verify the mouse is actually at the target position
                            var currentMousePos = GetMousePosition();
                            var distanceFromTarget = Vector2.Distance(currentMousePos, checkpointCenter);

                            if (distanceFromTarget < 10) // Within reasonable tolerance
                            {
                                BetterFollowbotLite.Instance.LogMessage($"AUTO RESPAWN: Mouse positioned correctly (distance: {distanceFromTarget:F1}), performing click");

                                // Perform the click with proper timing
                                Mouse.LeftClick();
                                System.Threading.Thread.Sleep(150); // Wait after click

                                // Verify click was successful by checking if panel is still visible
                                System.Threading.Thread.Sleep(500); // Give time for respawn to process

                                var panelStillVisible = resurrectPanel.IsVisible;
                                if (!panelStillVisible)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("AUTO RESPAWN: Checkpoint respawn successful - panel disappeared");
                                }
                                else
                                {
                                    BetterFollowbotLite.Instance.LogMessage("AUTO RESPAWN: Checkpoint respawn may have failed - panel still visible, retrying...");

                                    // Retry with a longer delay
                                    System.Threading.Thread.Sleep(300);
                                    Mouse.SetCursorPos(checkpointCenter);
                                    System.Threading.Thread.Sleep(300);
                                    Mouse.LeftClick();
                                    System.Threading.Thread.Sleep(200);
                                }

                                lastTimeAny = DateTime.Now; // Update global cooldown
                            }
                            else
                            {
                                BetterFollowbotLite.Instance.LogMessage($"AUTO RESPAWN: Mouse positioning failed - distance from target: {distanceFromTarget:F1}");
                            }
                        }
                        else
                        {
                            BetterFollowbotLite.Instance.LogMessage("AUTO RESPAWN: Checkpoint respawn button not available or not visible");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                BetterFollowbotLite.Instance.LogMessage($"AUTO RESPAWN: Exception occurred - {e.Message}");
            }

            #endregion

            #region Summon Skeletons

            if (Settings.summonSkeletonsEnabled && Gcd())
            {
                try
                {
                    // Check if we have a party leader to follow
                    var leaderPartyElement = PartyElements.GetPlayerInfoElementList()
                        .FirstOrDefault(x => string.Equals(x?.PlayerName?.ToLower(),
                            Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                    if (leaderPartyElement != null)
                    {
                        // Find the actual leader entity
                        var playerEntities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]
                            .Where(x => x != null && x.IsValid && !x.IsHostile);

                        var leaderEntity = playerEntities
                            .FirstOrDefault(x => string.Equals(x.GetComponent<Player>()?.PlayerName?.ToLower(),
                                Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                        if (leaderEntity != null)
                        {
                            // Check distance to leader
                            var distanceToLeader = Vector3.Distance(playerPosition, leaderEntity.Pos);

                            // Only summon if within range
                            if (distanceToLeader <= Settings.summonSkeletonsRange.Value)
                            {
                                // Count current skeletons
                                var skeletonCount = Summons.GetSkeletonCount();

                                // Summon if we have less than the minimum required
                                if (skeletonCount < Settings.summonSkeletonsMinCount.Value)
                                {
                                    // Find the summon skeletons skill
                                    var summonSkeletonsSkill = skills.FirstOrDefault(s =>
                                        s.Name.Contains("Summon Skeletons") ||
                                        s.Name.Contains("summon") && s.Name.Contains("skeleton"));

                                    if (summonSkeletonsSkill != null && summonSkeletonsSkill.IsOnSkillBar && summonSkeletonsSkill.CanBeUsed)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage($"SUMMON SKELETONS: Current: {skeletonCount}, Required: {Settings.summonSkeletonsMinCount.Value}, Distance to leader: {distanceToLeader:F1}");

                                        // Use the summon skeletons skill
                                        Keyboard.KeyPress(GetSkillInputKey(summonSkeletonsSkill.SkillSlotIndex));
                                        lastTimeAny = DateTime.Now; // Update global cooldown

                                        BetterFollowbotLite.Instance.LogMessage("SUMMON SKELETONS: Summoned skeletons successfully");
                                    }
                                    else if (summonSkeletonsSkill == null)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SUMMON SKELETONS: Summon Skeletons skill not found in skill bar");
                                    }
                                    else if (!summonSkeletonsSkill.CanBeUsed)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SUMMON SKELETONS: Summon Skeletons skill is on cooldown or unavailable");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    BetterFollowbotLite.Instance.LogMessage($"SUMMON SKELETONS: Exception occurred - {e.Message}");
                }
            }

            #endregion

            #region Auto Level Gems

            // Debug: Check if auto level gems is enabled
            if (Settings.autoLevelGemsEnabled.Value)
            {
                BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Setting value is true, checking GCD...");
            }

            if (Settings.autoLevelGemsEnabled && Gcd())
            {
                try
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Feature enabled, checking for gem level up panel...");

                    // Check if the gem level up panel is visible
                    var gemLvlUpPanel = GameController.IngameState.IngameUi.GemLvlUpPanel;
                    if (gemLvlUpPanel != null && gemLvlUpPanel.IsVisible)
                    {
                        // Get the array of gems to level up
                        var gemsToLvlUp = gemLvlUpPanel.GemsToLvlUp;
                        if (gemsToLvlUp != null && gemsToLvlUp.Count > 0)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Found {gemsToLvlUp.Count} gems available for leveling");

                            // Process each gem in the array
                            foreach (var gem in gemsToLvlUp)
                            {
                                if (gem != null && gem.IsVisible)
                                {
                                    try
                                    {
                                        // Get the children of the gem element
                                        var gemChildren = gem.Children;
                                        if (gemChildren != null && gemChildren.Count > 1)
                                        {
                                            // Get the second child ([1]) which contains the level up button
                                            var levelUpButton = gemChildren[1];
                                            if (levelUpButton != null && levelUpButton.IsVisible)
                                            {
                                                // Get the center position of the level up button
                                                var buttonRect = levelUpButton.GetClientRectCache;
                                                var buttonCenter = buttonRect.Center;

                                                BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Leveling up gem at position X: {buttonCenter.X:F1}, Y: {buttonCenter.Y:F1}");

                                                // Move mouse to the button and click
                                                Mouse.SetCursorPos(buttonCenter);

                                                // Wait for mouse to settle
                                                System.Threading.Thread.Sleep(150);

                                                // Verify mouse position
                                                var currentMousePos = GetMousePosition();
                                                var distanceFromTarget = Vector2.Distance(currentMousePos, buttonCenter);
                                                BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Mouse distance from target: {distanceFromTarget:F1}");

                                                if (distanceFromTarget < 5) // Close enough to target
                                                {
                                                    // Perform click with verification
                                                    BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Performing left click on level up button");

                                                    // First click attempt - use synchronous mouse events
                                                    Mouse.LeftMouseDown();
                                                    System.Threading.Thread.Sleep(40);
                                                    Mouse.LeftMouseUp();
                                                    System.Threading.Thread.Sleep(200);

                                                    // Check if button is still visible (if not, click was successful)
                                                    var buttonStillVisible = levelUpButton.IsVisible;
                                                    if (!buttonStillVisible)
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Click successful - button disappeared");
                                                    }
                                                    else
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Button still visible, attempting second click");

                                                        // Exponential backoff: wait longer before second attempt
                                                        System.Threading.Thread.Sleep(500);
                                                        Mouse.LeftMouseDown();
                                                        System.Threading.Thread.Sleep(40);
                                                        Mouse.LeftMouseUp();
                                                        System.Threading.Thread.Sleep(200);

                                                        // Final check
                                                        buttonStillVisible = levelUpButton.IsVisible;
                                                        if (!buttonStillVisible)
                                                        {
                                                            BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Second click successful");
                                                        }
                                                        else
                                                        {
                                                            BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Both clicks failed - button still visible");
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Mouse positioning failed - too far from target ({distanceFromTarget:F1})");
                                                }

                                                // Add delay between gem level ups
                                                System.Threading.Thread.Sleep(300);

                                                BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Gem level up attempt completed");

                                                // Update global cooldown after leveling a gem
                                                lastTimeAny = DateTime.Now;

                                                // Only level up one gem per frame to avoid spam
                                                break;
                                            }
                                            else
                                            {
                                                BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Level up button not found or not visible");
                                            }
                                        }
                                        else
                                        {
                                            BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: Gem children not found or insufficient count");
                                        }
                                    }
                                    catch (Exception gemEx)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Error processing individual gem - {gemEx.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            BetterFollowbotLite.Instance.LogMessage("AUTO LEVEL GEMS: No gems available for leveling");
                        }
                    }
                }
                catch (Exception e)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO LEVEL GEMS: Exception occurred - {e.Message}");
                }
            }

            #endregion

            #region Auto Join Party

            // Check if auto join party is enabled and enough time has passed since last attempt (0.5 second cooldown)
            var timeSinceLastAttempt = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
            if (Settings.autoJoinPartyEnabled && timeSinceLastAttempt >= 0.5 && Gcd())
            {
                // Only log every 10 seconds to avoid spam
                if (timeSinceLastAttempt >= 10.0 || lastAutoJoinPartyAttempt == DateTime.MinValue)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Active - checking for party invites");
                }
                try
                {
                    // Check if player is already in a party - if so, don't accept invites
                    var partyElement = PartyElements.GetPlayerInfoElementList();
                    var isInParty = partyElement != null && partyElement.Count > 0;

                    if (isInParty)
                    {
                        // Only log this occasionally to avoid spam (every 15 seconds)
                        if (timeSinceLastAttempt >= 15.0)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Player already in party ({partyElement.Count} members)");
                        }
                        // Still update the cooldown to prevent spam
                        lastAutoJoinPartyAttempt = DateTime.Now;
                        return;
                    }

                    // Check if the invites panel is visible
                    var invitesPanel = GameController.IngameState.IngameUi.InvitesPanel;
                    if (invitesPanel != null && invitesPanel.IsVisible)
                    {
                        // Only log when we actually find an invite (less frequent)
                        BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Party invite detected - attempting to accept");

                        // Get the children for navigation
                        var children = invitesPanel.Children;

                        // Navigate the UI hierarchy as originally specified: InvitesPanel -> Children[0] -> Children[2] -> Children[0]
                        if (children != null && children.Count > 0)
                        {
                            var firstChild = children[0];
                            if (firstChild != null && firstChild.Children != null && firstChild.Children.Count > 2)
                            {
                                var secondChild = firstChild.Children[2];
                                if (secondChild != null && secondChild.Children != null && secondChild.Children.Count > 0)
                                {
                                    var acceptButton = secondChild.Children[0];
                                    if (acceptButton != null && acceptButton.IsVisible)
                                    {
                                        // Get the center position of the accept button
                                        var buttonRect = acceptButton.GetClientRectCache;
                                        var buttonCenter = buttonRect.Center;

                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Accept button position - X: {buttonCenter.X:F1}, Y: {buttonCenter.Y:F1}");

                                        // Move mouse to the accept button
                                        Mouse.SetCursorPos(buttonCenter);

                                        // Wait for mouse to settle - longer delay to avoid AutoPilot interference
                                        System.Threading.Thread.Sleep(300);

                                        // Verify mouse position
                                        var currentMousePos = GetMousePosition();
                                        var distanceFromTarget = Vector2.Distance(currentMousePos, buttonCenter);
                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Mouse distance from target: {distanceFromTarget:F1}");

                                        if (distanceFromTarget < 15) // Allow slightly more tolerance
                                        {
                                            // Perform click with verification
                                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Performing left click on accept button");

                                            // First click attempt - use synchronous mouse events
                                            Mouse.LeftMouseDown();
                                            System.Threading.Thread.Sleep(40);
                                            Mouse.LeftMouseUp();
                                            System.Threading.Thread.Sleep(300); // Longer delay

                                            // Check if we successfully joined a party
                                            var partyAfterClick = PartyElements.GetPlayerInfoElementList();
                                            var joinedParty = partyAfterClick != null && partyAfterClick.Count > 0;

                                            if (joinedParty)
                                            {
                                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Successfully joined party!");
                                            }
                                            else
                                            {
                                                // Second click attempt with longer delay
                                                System.Threading.Thread.Sleep(600);
                                                Mouse.LeftMouseDown();
                                                System.Threading.Thread.Sleep(40);
                                                Mouse.LeftMouseUp();
                                                System.Threading.Thread.Sleep(300);

                                                // Check again
                                                partyAfterClick = PartyElements.GetPlayerInfoElementList();
                                                joinedParty = partyAfterClick != null && partyAfterClick.Count > 0;

                                                if (joinedParty)
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Successfully joined party on second attempt!");
                                                }
                                                else
                                                {
                                                    // Only log failures occasionally to avoid spam
                                                    var timeSinceLastFailure = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                                                    if (timeSinceLastFailure >= 30.0)
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Failed to join party - may need manual intervention");
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Mouse positioning failed - too far from target ({distanceFromTarget:F1})");
                                        }

                                        // Update cooldowns
                                        lastTimeAny = DateTime.Now;
                                        lastAutoJoinPartyAttempt = DateTime.Now;
                                    }
                                    else
                                    {
                                        // Only log button not found occasionally to avoid spam
                                        var timeSinceLastLog = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                                        if (timeSinceLastLog >= 20.0)
                                        {
                                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Accept button not found or not visible");
                                        }
                                    }
                                }
                                else
                                {
                                    // Only log navigation failures occasionally to avoid spam
                                    var timeSinceLastLog = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                                    if (timeSinceLastLog >= 30.0)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: UI hierarchy navigation failed");
                                    }
                                }
                            }
                            else
                            {
                                var timeSinceLastLog = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                                if (timeSinceLastLog >= 30.0)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: UI hierarchy navigation failed");
                                }
                            }
                        }
                        else
                        {
                            var timeSinceLastLog = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                            if (timeSinceLastLog >= 30.0)
                            {
                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Invites panel has no children");
                            }
                        }
                    }
                    else
                    {
                        // Don't log when no invites are present - this is normal operation
                        // Only log occasionally if there might be an issue
                        var timeSinceLastLog = (DateTime.Now - lastAutoJoinPartyAttempt).TotalSeconds;
                        if (timeSinceLastLog >= 60.0) // Log once per minute when no invites
                        {
                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: No party invites detected");
                        }
                    }
                }
                catch (Exception e)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Exception occurred - {e.Message}");
                }
            }

            #endregion

            #region Auto Map Tabber

            try
            {
                if (Settings.autoMapTabber && !Keyboard.IsKeyDown((int)Settings.inputKeyPickIt.Value))
                    if (SkillInfo.ManageCooldown(SkillInfo.autoMapTabber))
                    {
                        bool shouldBeClosed = GameController.IngameState.IngameUi.Atlas.IsVisible ||
                                              GameController.IngameState.IngameUi.AtlasTreePanel.IsVisible ||
                                              GameController.IngameState.IngameUi.StashElement.IsVisible ||
                                              GameController.IngameState.IngameUi.TradeWindow.IsVisible || 
                                              GameController.IngameState.IngameUi.ChallengesPanel.IsVisible ||
                                              GameController.IngameState.IngameUi.CraftBench.IsVisible ||
                                              GameController.IngameState.IngameUi.DelveWindow.IsVisible ||
                                              GameController.IngameState.IngameUi.ExpeditionWindow.IsVisible || 
                                              GameController.IngameState.IngameUi.BanditDialog.IsVisible ||
                                              GameController.IngameState.IngameUi.MetamorphWindow.IsVisible ||
                                              GameController.IngameState.IngameUi.SyndicatePanel.IsVisible || 
                                              GameController.IngameState.IngameUi.SyndicateTree.IsVisible ||
                                              GameController.IngameState.IngameUi.QuestRewardWindow.IsVisible ||
                                              GameController.IngameState.IngameUi.SynthesisWindow.IsVisible ||
                                              //GameController.IngameState.IngameUi.UltimatumPanel.IsVisible || 
                                              GameController.IngameState.IngameUi.MapDeviceWindow.IsVisible ||
                                              GameController.IngameState.IngameUi.SellWindow.IsVisible ||
                                              GameController.IngameState.IngameUi.SettingsPanel.IsVisible ||
                                              GameController.IngameState.IngameUi.InventoryPanel.IsVisible || 
                                              //GameController.IngameState.IngameUi.NpcDialog.IsVisible ||
                                              GameController.IngameState.IngameUi.TreePanel.IsVisible;
                           
                            
                        if (!GameController.IngameState.IngameUi.Map.SmallMiniMap.IsVisibleLocal && shouldBeClosed)
                        {
                            Keyboard.KeyPress(Keys.Tab);
                            SkillInfo.autoMapTabber.Cooldown = 250;
                        }
                        else if (GameController.IngameState.IngameUi.Map.SmallMiniMap.IsVisibleLocal && !shouldBeClosed)
                        {
                            Keyboard.KeyPress(Keys.Tab);
                            SkillInfo.autoMapTabber.Cooldown = 250;
                        }
                    } 
            }
            catch (Exception e)
            {
                // Error handling without logging
            }

            #endregion
            if (GameController.Area.CurrentArea.IsHideout || GameController.Area.CurrentArea.IsTown ||
                /*GameController.IngameState.IngameUi.StashElement.IsVisible ||*/ // 3.15 Null
                GameController.IngameState.IngameUi.NpcDialog.IsVisible ||
                GameController.IngameState.IngameUi.SellWindow.IsVisible || MenuWindow.IsOpened ||
                !GameController.InGame || GameController.IsLoading) return;
                
            enemys = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster].Where(x =>
                x != null && x.IsAlive && x.IsHostile && x.GetComponent<Life>()?.CurHP > 0 && 
                x.GetComponent<Targetable>()?.isTargetable == true && !HasStat(x, GameStat.CannotBeDamaged) &&
                GameController.Window.GetWindowRectangleTimeCache.Contains(
                    GameController.Game.IngameState.Camera.WorldToScreen(x.Pos))).ToList();
            if (Settings.debugMode)
            {
                Graphics.DrawText("Enemys: " + enemys.Count, new System.Numerics.Vector2(100, 120), Color.White);
            }
                
            // Corpses collection removed since no longer needed



                

            // Do not Cast anything while we are untouchable or Chat is Open
            if (buffs.Exists(x => x.Name == "grace_period") ||
                /*GameController.IngameState.IngameUi.ChatBoxRoot.Parent.Parent.Parent.GetChildAtIndex(3).IsVisible || */ // 3.15 Bugged 
                !GameController.IsForeGroundCache)
                return;
                
            foreach (var skill in skills.Where(skill => skill.IsOnSkillBar && skill.SkillSlotIndex >= 1 && skill.SkillSlotIndex != 2 && skill.CanBeUsed))
            {
                    

                #region Aura Blessing

                if (Settings.auraBlessingEnabled)
                {
                    try
                    {
                        // Holy Relic summoning logic
                        if (skill.Id == SkillInfo.holyRelict.Id)
                        {
                            // Check cooldown to prevent double-spawning
                            if (SkillInfo.ManageCooldown(SkillInfo.holyRelict, skill))
                            {
                                var lowestMinionHp = Summons.GetLowestMinionHpp();
                                // Convert HP percentage from 0-1 range to 0-100 range for comparison
                                var lowestMinionHpPercent = lowestMinionHp * 100f;
                                // Check for Holy Relic minion presence
                                // Prioritize ReAgent buff names, then check for other indicators
                                // Note: Avoid "guardian_life_regen" as it's just the life regen effect, not minion presence
                                var hasGuardianBlessingMinion = buffs.Exists(x =>
                                    x.Name == "has_guardians_blessing_minion" ||
                                    (x.Name.Contains("holy") && x.Name.Contains("relic") && !x.Name.Contains("life")) ||
                                    x.Name.Contains("guardian_blessing_minion"));

                                // Check conditions
                                var healthLow = lowestMinionHpPercent < Settings.holyRelicHealthThreshold;
                                var missingBuff = !hasGuardianBlessingMinion;

                                // If Holy Relic health is below threshold OR we don't have any minion buff, summon new Holy Relic
                                if (healthLow || missingBuff)
                                {
                                    Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                    SkillInfo.holyRelict.Cooldown = 200; // 2 second cooldown to prevent double-spawning
                                }
                            }
                        }

                        // Zealotry casting logic
                        else if (skill.Id == SkillInfo.auraZealotry.Id)
                        {
                            // Check for Zealotry aura buff
                            // Prioritize ReAgent buff names, then check for aura effects
                            var hasGuardianBlessingAura = buffs.Exists(x =>
                                x.Name == "has_guardians_blessing_aura" ||
                                x.Name == "zealotry" ||
                                x.Name == "player_aura_spell_damage" ||
                                (x.Name.Contains("blessing") && x.Name.Contains("aura")));

                            // Check for Holy Relic minion presence (same logic as Holy Relic section)
                            var hasGuardianBlessingMinion = buffs.Exists(x =>
                                x.Name == "has_guardians_blessing_minion" ||
                                (x.Name.Contains("holy") && x.Name.Contains("relic") && !x.Name.Contains("life")) ||
                                x.Name.Contains("guardian_blessing_minion"));

                            // Check conditions
                            var missingAura = !hasGuardianBlessingAura;
                            var hasMinion = hasGuardianBlessingMinion;

                            // If we have the minion but don't have the aura buff, cast Zealotry
                            if (missingAura && hasMinion)
                            {
                                Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }
                }

                #endregion


                #region Link Skills

                if (Settings.flameLinkEnabled)
                    try
                    {
                        if (skill.Id == SkillInfo.flameLink.Id)
                        {
                            var linkSkill = SkillInfo.flameLink;
                            var targetBuffName = "flame_link_target";

                            if (SkillInfo.ManageCooldown(linkSkill, skill))
                            {
                                // Get party leader
                                var partyElements = PartyElements.GetPlayerInfoElementList();

                                var leaderPartyElement = partyElements
                                    .FirstOrDefault(x => string.Equals(x?.PlayerName?.ToLower(),
                                        Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                                if (leaderPartyElement != null)
                                {
                                    // Find the actual player entity by name
                                    var playerEntities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]
                                        .Where(x => x != null && x.IsValid && !x.IsHostile);

                                    var leaderEntity = playerEntities
                                        .FirstOrDefault(x => string.Equals(x.GetComponent<Player>()?.PlayerName?.ToLower(),
                                            Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                                    if (leaderEntity != null)
                                    {
                                        // Set the player entity
                                        leaderPartyElement.Data.PlayerEntity = leaderEntity;

                                        var leader = leaderPartyElement.Data.PlayerEntity;
                                        var leaderBuffs = leader.GetComponent<Buffs>().BuffsList;

                                        // Check if leader has the target buff
                                        var hasLinkTarget = leaderBuffs.Exists(x => x.Name == targetBuffName);

                                        // Check if we have the source buff and its timer
                                        var linkSourceBuff = buffs.FirstOrDefault(x => x.Name == linkSkill.BuffName);
                                        var linkSourceTimeLeft = linkSourceBuff?.Timer ?? 0;

                                        // Check distance from leader to mouse cursor in screen space
                                        var mouseScreenPos = GetMousePosition();
                                        var leaderScreenPos = Helper.WorldToValidScreenPosition(leader.Pos);
                                        var distanceToCursor = Vector2.Distance(mouseScreenPos, leaderScreenPos);

                                        // Logic: Aggressive flame link maintenance - refresh much earlier and with larger distance tolerance
                                        // Emergency linking (no source buff): ignore distance
                                        // Normal linking: use distance check
                                        var shouldActivate = (!hasLinkTarget || linkSourceTimeLeft < 8 || linkSourceBuff == null) &&
                                                             (linkSourceBuff == null || distanceToCursor < 100);

                                        if (shouldActivate)
                                        {
                                            // Move mouse to leader position
                                            var leaderScreenPosForMouse = GameController.IngameState.Camera.WorldToScreen(leader.Pos);
                                            Mouse.SetCursorPos(leaderScreenPosForMouse);

                                            // Activate the skill
                                            Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                            linkSkill.Cooldown = 100;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }

                #endregion

                #region Smite Buff

                if (Settings.smiteEnabled)
                    try
                    {
                        if (skill.Id == SkillInfo.smite.Id)
                        {
                            BetterFollowbotLite.Instance.LogMessage("SMITE: Smite skill detected");

                            // Custom cooldown check for smite that bypasses GCD since it's a buff skill
                            if (SkillInfo.smite.Cooldown <= 0 &&
                                !(skill.RemainingUses <= 0 && skill.IsOnCooldown))
                            {
                                // Check mana cost
                                if (!skill.Stats.TryGetValue(GameStat.ManaCost, out var manaCost))
                                    manaCost = 0;

                                if (BetterFollowbotLite.Instance.player.CurMana >= manaCost ||
                                    (BetterFollowbotLite.Instance.localPlayer.Stats.TryGetValue(GameStat.VirtualEnergyShieldProtectsMana, out var hasEldritchBattery) &&
                                     hasEldritchBattery > 0 && (BetterFollowbotLite.Instance.player.CurES + BetterFollowbotLite.Instance.player.CurMana) >= manaCost))
                                {
                                    BetterFollowbotLite.Instance.LogMessage("SMITE: Cooldown check passed");

                                    // Check if we don't have the smite buff or it's about to expire
                                var smiteBuff = buffs.FirstOrDefault(x => x.Name == "smite_buff");
                                var hasSmiteBuff = smiteBuff != null;
                                var buffTimeLeft = smiteBuff?.Timer ?? 0;
                                BetterFollowbotLite.Instance.LogMessage($"SMITE: Has smite buff: {hasSmiteBuff}, Time left: {buffTimeLeft:F1}s");

                                // Refresh if no buff or buff has less than 2 seconds left
                                if (!hasSmiteBuff || buffTimeLeft < 2.0f)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("SMITE: No smite buff found, looking for targets");

                                    // Find monsters within 250 units of player (smite attack range)
                                    var targetMonster = enemys
                                        .Where(monster =>
                                        {
                                            // Check if monster is within 250 units of player
                                            var distanceToPlayer = Vector3.Distance(playerPosition, monster.Pos);
                                            // Check if monster is on screen (can be targeted)
                                            var screenPos = GameController.IngameState.Camera.WorldToScreen(monster.Pos);
                                            var isOnScreen = GameController.Window.GetWindowRectangleTimeCache.Contains(screenPos);
                                            BetterFollowbotLite.Instance.LogMessage($"SMITE: Monster at distance {distanceToPlayer:F1} from player, on screen: {isOnScreen}");
                                            return distanceToPlayer <= 250 && isOnScreen;
                                        })
                                        .OrderBy(monster => Vector3.Distance(playerPosition, monster.Pos)) // Closest first
                                        .FirstOrDefault();

                                    if (targetMonster != null)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SMITE: Found suitable target, activating smite!");

                                        // Move mouse to monster position
                                        var monsterScreenPos = GameController.IngameState.Camera.WorldToScreen(targetMonster.Pos);
                                        Mouse.SetCursorPos(monsterScreenPos);

                                        // Small delay to ensure mouse movement is registered
                                        System.Threading.Thread.Sleep(50);

                                        // Double-check mouse position is still valid
                                        var currentMousePos = GetMousePosition();
                                        var distanceFromTarget = Vector2.Distance(currentMousePos, monsterScreenPos);
                                        if (distanceFromTarget < 50) // Within reasonable tolerance
                                        {
                                            // Activate the skill
                                            Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                            SkillInfo.smite.Cooldown = 100;
                                            lastTimeAny = DateTime.Now; // Update global cooldown
                                            BetterFollowbotLite.Instance.LogMessage("SMITE: Smite activated successfully");
                                        }
                                        else
                                        {
                                            BetterFollowbotLite.Instance.LogMessage($"SMITE: Mouse positioning failed, distance: {distanceFromTarget:F1}");
                                        }
                                    }
                                    else
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SMITE: No suitable targets found within range, dashing to leader");

                                        // Dash to leader to get near monsters
                                        if (Settings.autoPilotDashEnabled && (DateTime.Now - autoPilot.lastDashTime).TotalMilliseconds >= 3000 && autoPilot.FollowTarget != null)
                                        {
                                            var leaderPos = autoPilot.FollowTarget.Pos;
                                            var distanceToLeader = Vector3.Distance(playerPosition, leaderPos);

                                            // CRITICAL: Don't dash if teleport is in progress (strongest protection)
                                            if (AutoPilot.IsTeleportInProgress)
                                            {
                                                BetterFollowbotLite.Instance.LogMessage("SMITE: TELEPORT IN PROGRESS - blocking all dash attempts");
                                            }
                                            else
                                            {
                                                // Fallback: Check for transition tasks
                                                var hasTransitionTask = autoPilot.Tasks.Any(t =>
                                                    t.Type == TaskNodeType.Transition ||
                                                    t.Type == TaskNodeType.TeleportConfirm ||
                                                    t.Type == TaskNodeType.TeleportButton);

                                                if (hasTransitionTask)
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage($"SMITE: Transition/teleport task active ({autoPilot.Tasks.Count} tasks), skipping dash");
                                                }
                                                else if (distanceToLeader > 50) // Only dash if we're not already close to leader
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage($"SMITE: Dashing to leader - Distance: {distanceToLeader:F1}");

                                                    // Position mouse towards leader
                                                    var leaderScreenPos = GameController.IngameState.Camera.WorldToScreen(leaderPos);
                                                    Mouse.SetCursorPos(leaderScreenPos);

                                                    // Small delay to ensure mouse movement is registered
                                                    System.Threading.Thread.Sleep(50);

                                                    // Execute dash
                                                    Keyboard.KeyPress(Settings.autoPilotDashKey);
                                                    autoPilot.lastDashTime = DateTime.Now;

                                                    BetterFollowbotLite.Instance.LogMessage("SMITE: Dash to leader executed");
                                                }
                                                else
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage("SMITE: Already close to leader, skipping dash");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            BetterFollowbotLite.Instance.LogMessage("SMITE: Dash not available or not enabled");
                                        }
                                    }
                                }
                                    else
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SMITE: Already have smite buff, skipping");
                                    }
                                }
                                else
                                {
                                    BetterFollowbotLite.Instance.LogMessage("SMITE: Not enough mana for smite");
                                }
                            }
                            else
                            {
                                BetterFollowbotLite.Instance.LogMessage("SMITE: Cooldown check failed");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }
                else
                {
                    BetterFollowbotLite.Instance.LogMessage("SMITE: Smite is not enabled");
                }

                #endregion

                #region Vaal Skills

                if (Settings.vaalHasteEnabled)
                    try
                    {
                        if (skill.Id == SkillInfo.vaalHaste.Id)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"VAAL HASTE: Vaal Haste skill detected - ID: {skill.Id}, Name: {skill.Name}");

                            // Vaal skills use charges, not traditional cooldowns
                            if (!(skill.RemainingUses <= 0 && skill.IsOnCooldown))
                            {
                                // Check if we don't already have the vaal haste buff
                                var hasVaalHasteBuff = buffs.Exists(x => x.Name == "vaal_haste");
                                BetterFollowbotLite.Instance.LogMessage($"VAAL HASTE: Has vaal haste buff: {hasVaalHasteBuff}");

                                if (!hasVaalHasteBuff)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("VAAL HASTE: No vaal haste buff found, activating");

                                    // Activate the skill
                                    Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));

                                    BetterFollowbotLite.Instance.LogMessage("VAAL HASTE: Vaal Haste activated successfully");
                                }
                                else
                                {
                                    BetterFollowbotLite.Instance.LogMessage("VAAL HASTE: Already have vaal haste buff, skipping");
                                }
                            }
                            else
                            {
                                BetterFollowbotLite.Instance.LogMessage("VAAL HASTE: Cooldown check failed");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }

                if (Settings.vaalDisciplineEnabled)
                    try
                    {
                        if (skill.Id == SkillInfo.vaalDiscipline.Id)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"VAAL DISCIPLINE: Vaal Discipline skill detected - ID: {skill.Id}, Name: {skill.Name}");

                            // Vaal skills use charges, not traditional cooldowns
                            if (!(skill.RemainingUses <= 0 && skill.IsOnCooldown))
                            {
                                // Check if ES is below threshold
                                var esPercentage = player.ESPercentage;
                                var threshold = (float)Settings.vaalDisciplineEsp / 100;

                                BetterFollowbotLite.Instance.LogMessage($"VAAL DISCIPLINE: ES%: {esPercentage:F1}, Threshold: {threshold:F2}");

                                if (esPercentage < threshold)
                                {
                                    // Check if we don't already have the vaal discipline buff
                                    var hasVaalDisciplineBuff = buffs.Exists(x => x.Name == "vaal_discipline");
                                    BetterFollowbotLite.Instance.LogMessage($"VAAL DISCIPLINE: Has vaal discipline buff: {hasVaalDisciplineBuff}");

                                    if (!hasVaalDisciplineBuff)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("VAAL DISCIPLINE: ES below threshold and no buff found, activating");

                                        // Activate the skill
                                        Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));

                                        BetterFollowbotLite.Instance.LogMessage("VAAL DISCIPLINE: Vaal Discipline activated successfully");
                                    }
                                    else
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("VAAL DISCIPLINE: Already have vaal discipline buff, skipping");
                                    }
                                }
                                else
                                {
                                    BetterFollowbotLite.Instance.LogMessage("VAAL DISCIPLINE: ES above threshold, skipping");
                                }
                            }
                            else
                            {
                                BetterFollowbotLite.Instance.LogMessage("VAAL DISCIPLINE: Cooldown check failed");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }

                #endregion

                #region Mines

                if (Settings.minesEnabled)
                    try
                    {
                        // Check if we have either stormblast or pyroclast mine skills enabled
                        var hasStormblastMine = Settings.minesStormblastEnabled && skill.Id == SkillInfo.stormblastMine.Id;
                        var hasPyroclastMine = Settings.minesPyroclastEnabled && skill.Id == SkillInfo.pyroclastMine.Id;

                        if (hasStormblastMine || hasPyroclastMine)
                        {
                            // Check cooldown
                            var mineSkill = hasStormblastMine ? SkillInfo.stormblastMine : SkillInfo.pyroclastMine;
                            if (SkillInfo.ManageCooldown(mineSkill, skill))
                            {
                                // Find nearby rare/unique enemies within range
                                var nearbyRareUniqueEnemies = enemys
                                    .Where(monster =>
                                    {
                                        // Check if monster is rare or unique
                                        if (monster.Rarity != MonsterRarity.Rare && monster.Rarity != MonsterRarity.Unique)
                                            return false;

                                        // Check distance from player to monster
                                        var distanceToMonster = Vector2.Distance(
                                            new Vector2(monster.PosNum.X, monster.PosNum.Y),
                                            new Vector2(playerPosition.X, playerPosition.Y));

                                        // Parse mines range from text input, default to 35 if invalid
                                        if (!int.TryParse(Settings.minesRange.Value, out var minesRange))
                                            minesRange = 35;

                                        return distanceToMonster <= minesRange;
                                    })
                                    .ToList();

                                if (nearbyRareUniqueEnemies.Any())
                                {
                                    // Check if we're close to the party leader
                                    var shouldThrowMine = false;
                                    var leaderPos = Vector2.Zero;

                                    if (!string.IsNullOrEmpty(Settings.autoPilotLeader.Value))
                                    {
                                        // Get party elements
                                        var partyElements = PartyElements.GetPlayerInfoElementList();
                                        var leaderPartyElement = partyElements
                                            .FirstOrDefault(x => string.Equals(x?.PlayerName?.ToLower(),
                                                Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                                        if (leaderPartyElement != null)
                                        {
                                            // Find the actual player entity by name
                                            var playerEntities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player]
                                                .Where(x => x != null && x.IsValid && !x.IsHostile);

                                            var leaderEntity = playerEntities
                                                .FirstOrDefault(x => string.Equals(x.GetComponent<Player>()?.PlayerName?.ToLower(),
                                                    Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase));

                                            if (leaderEntity != null)
                                            {
                                                // Check distance to leader
                                                var distanceToLeader = Vector2.Distance(
                                                    new Vector2(playerPosition.X, playerPosition.Y),
                                                    new Vector2(leaderEntity.Pos.X, leaderEntity.Pos.Y));

                                                // Parse leader distance from text input, default to 50 if invalid
                                                if (!int.TryParse(Settings.minesLeaderDistance.Value, out var leaderDistance))
                                                    leaderDistance = 50;

                                                if (distanceToLeader <= leaderDistance)
                                                {
                                                    shouldThrowMine = true;
                                                    leaderPos = new Vector2(leaderEntity.Pos.X, leaderEntity.Pos.Y);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // If no leader set, always throw mines when enemies are nearby
                                        shouldThrowMine = true;
                                    }

                                    if (shouldThrowMine)
                                    {
                                        // Find the best position to throw the mine (near enemies but not too close to leader if we have one)
                                        var bestTarget = nearbyRareUniqueEnemies
                                            .OrderBy(monster =>
                                            {
                                                var monsterPos = new Vector2(monster.PosNum.X, monster.PosNum.Y);
                                                var distanceToMonster = Vector2.Distance(new Vector2(playerPosition.X, playerPosition.Y), monsterPos);

                                                // If we have a leader, prefer targets that are closer to the leader
                                                if (leaderPos != Vector2.Zero)
                                                {
                                                    var distanceToLeader = Vector2.Distance(monsterPos, leaderPos);
                                                    return distanceToLeader + distanceToMonster * 0.5f; // Weight both distances
                                                }

                                                return distanceToMonster;
                                            })
                                            .FirstOrDefault();

                                        if (bestTarget != null)
                                        {
                                            // Move mouse to target position
                                            var targetScreenPos = GameController.IngameState.Camera.WorldToScreen(bestTarget.Pos);
                                            Mouse.SetCursorPos(targetScreenPos);

                                            // Small delay to ensure mouse movement is registered
                                            System.Threading.Thread.Sleep(50);

                                            // Activate the mine skill
                                            Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                            mineSkill.Cooldown = 100; // Set cooldown to prevent spam
                                            lastTimeAny = DateTime.Now;

                                            if (Settings.debugMode)
                                            {
                                                LogMessage($"MINES: Threw {(hasStormblastMine ? "Stormblast" : "Pyroclast")} mine at {bestTarget.Path} (Rarity: {bestTarget.Rarity})");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }

                #endregion

                /*
                #region Spider

                if (false)
                {
                    if (skill.Id == SkillInfo.summonSpiders.Id && SkillInfo.ManageCooldown(SkillInfo.summonSpiders, skill))
                    {
                        var spidersSummoned = buffs.Count(x => x.Name == SkillInfo.summonSpiders.BuffName);

                        if (spidersSummoned < 20 && GetCorpseWithin(30) >= 2)
                        {

                        }
                    }
                }


                #endregion
                */
                #region Detonate Mines ( to be done )
/*
                    if (Settings.minesEnabled)
                    {
                        try
                        {
                            var remoteMines = localPlayer.GetComponent<Actor>().DeployedObjects.Where(x =>
                                    x.Entity != null && x.Entity.Path == "Metadata/MiscellaneousObjects/RemoteMine")
                                .ToList();

                            // Removed Logic
                            // What should a proper Detonator do and when ?
                            // Detonate Mines when they have the chance to hit a target (Range), include min. mines ?
                            // Internal delay 500-1000ms ?
                        }
                        catch (Exception e)
                        {
                            // Error handling without logging
                        }
                    }
                    */
                #endregion
            }

        }
        catch (Exception e)
        {
            // Error handling without logging
        }
    }

    // Taken from ->
    // https://www.reddit.com/r/pathofexiledev/comments/787yq7/c_logout_app_same_method_as_lutbot/
        
    // Wont work when Private, No Touchy Touchy !!!
    // ReSharper disable once MemberCanBePrivate.Global
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
    public static partial class CommandHandler
    {
        public static void KillTcpConnectionForProcess(int processId)
        {
            MibTcprowOwnerPid[] table;
            const int afInet = 2;
            var buffSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref buffSize, true, afInet, TableClass.TcpTableOwnerPidAll);
            var buffTable = Marshal.AllocHGlobal(buffSize);
            try
            {
                var ret = GetExtendedTcpTable(buffTable, ref buffSize, true, afInet, TableClass.TcpTableOwnerPidAll);
                if (ret != 0)
                    return;
                var tab = (MibTcptableOwnerPid)Marshal.PtrToStructure(buffTable, typeof(MibTcptableOwnerPid));
                var rowPtr = (IntPtr)((long)buffTable + Marshal.SizeOf(tab.dwNumEntries));
                table = new MibTcprowOwnerPid[tab.dwNumEntries];
                for (var i = 0; i < tab.dwNumEntries; i++)
                {
                    var tcpRow = (MibTcprowOwnerPid)Marshal.PtrToStructure(rowPtr, typeof(MibTcprowOwnerPid));
                    table[i] = tcpRow;
                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(tcpRow));

                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffTable);
            }

            //Kill Path Connection
            var pathConnection = table.FirstOrDefault(t => t.owningPid == processId);
            pathConnection.state = 12;
            var ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(pathConnection));
            Marshal.StructureToPtr(pathConnection, ptr, false);
            SetTcpEntry(ptr);


        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TableClass tblClass, uint reserved = 0);

        [DllImport("iphlpapi.dll")]
        private static extern int SetTcpEntry(IntPtr pTcprow);

        [StructLayout(LayoutKind.Sequential)]
        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public struct MibTcprowOwnerPid
        {
            public uint state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] remotePort;
            public uint owningPid;

        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MibTcptableOwnerPid
        {
            public uint dwNumEntries;
            private readonly MibTcprowOwnerPid table;
        }

        private enum TableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }
    }
}