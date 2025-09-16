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
        SkillInfo.ResetSkills();
        skills = null;

        var coroutine = new Coroutine(WaitForSkillsAfterAreaChange(), this);
        Core.ParallelRunner.Run(coroutine);
            
        autoPilot.AreaChange();
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
                if (Settings.autoPilotEnabled && Settings.autoPilotGrace && buffs != null && buffs.Exists(x => x.Name == "grace_period") && Gcd())
                {
                    Keyboard.KeyPress(Settings.autoPilotMoveKey);
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
                    BetterFollowbotLite.Instance.LogMessage($"AURA BLESSING: Processing skill ID {skill.Id}, Name: {skill.Name}, Slot: {skill.SkillSlotIndex}");

                    // Log current relevant buffs for debugging (excluding life regen effects)
                    var relevantBuffs = buffs.Where(b =>
                        (b.Name.Contains("blessing") && !b.Name.Contains("life_regen")) ||
                        (b.Name.Contains("holy") && !b.Name.Contains("life")) ||
                        (b.Name.Contains("relic") && !b.Name.Contains("life")) ||
                        b.Name.Contains("zealotry") ||
                        b.Name.Contains("aura_spell_damage")).ToList();
                    if (relevantBuffs.Any())
                    {
                        BetterFollowbotLite.Instance.LogMessage($"AURA BLESSING: Current relevant buffs: {string.Join(", ", relevantBuffs.Select(b => $"{b.Name}({b.Timer:F1}s)"))}");
                    }
                    else
                    {
                        BetterFollowbotLite.Instance.LogMessage("AURA BLESSING: No relevant buffs found");
                    }

                    try
                    {
                        // Holy Relic summoning logic
                        if (skill.Id == SkillInfo.holyRelict.Id)
                        {
                            // Check cooldown to prevent double-spawning
                            if (SkillInfo.ManageCooldown(SkillInfo.holyRelict, skill))
                            {
                                BetterFollowbotLite.Instance.LogMessage($"HOLY RELIC: Detected Holy Relic skill (ID: {skill.Id}), CanBeUsed: {skill.CanBeUsed}, RemainingUses: {skill.RemainingUses}, IsOnCooldown: {skill.IsOnCooldown}");

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
                                var threshold = Settings.holyRelicHealthThreshold;

                                BetterFollowbotLite.Instance.LogMessage($"HOLY RELIC: Lowest minion HP: {lowestMinionHpPercent:F1}%, Threshold: {threshold}%, Has minion buff: {hasGuardianBlessingMinion}");

                                // Check conditions
                                var healthLow = lowestMinionHpPercent < threshold;
                                var missingBuff = !hasGuardianBlessingMinion;

                                BetterFollowbotLite.Instance.LogMessage($"HOLY RELIC: Health low: {healthLow}, Missing buff: {missingBuff}, Should summon: {healthLow || missingBuff}");

                                // If Holy Relic health is below threshold OR we don't have any minion buff, summon new Holy Relic
                                if (healthLow || missingBuff)
                                {
                                    BetterFollowbotLite.Instance.LogMessage($"HOLY RELIC: Summoning new Holy Relic (reason: {(healthLow ? "health low" : "missing buff")})");
                                    Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                    SkillInfo.holyRelict.Cooldown = 200; // 2 second cooldown to prevent double-spawning
                                }
                                else
                                {
                                    BetterFollowbotLite.Instance.LogMessage("HOLY RELIC: Conditions not met, not summoning");
                                }
                            }
                        }

                        // Zealotry casting logic
                        else if (skill.Id == SkillInfo.auraZealotry.Id)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"ZEALOTRY: Detected Zealotry skill (ID: {skill.Id}), CanBeUsed: {skill.CanBeUsed}, RemainingUses: {skill.RemainingUses}, IsOnCooldown: {skill.IsOnCooldown}");

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

                            BetterFollowbotLite.Instance.LogMessage($"ZEALOTRY: Has aura buff: {hasGuardianBlessingAura}, Has minion buff: {hasGuardianBlessingMinion}");

                            // Check conditions
                            var missingAura = !hasGuardianBlessingAura;
                            var hasMinion = hasGuardianBlessingMinion;

                            BetterFollowbotLite.Instance.LogMessage($"ZEALOTRY: Missing aura: {missingAura}, Has minion: {hasMinion}, Should cast: {missingAura && hasMinion}");

                            // If we have the minion but don't have the aura buff, cast Zealotry
                            if (missingAura && hasMinion)
                            {
                                BetterFollowbotLite.Instance.LogMessage("ZEALOTRY: Casting Zealotry (have minion but missing aura)");
                                Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                            }
                            else
                            {
                                BetterFollowbotLite.Instance.LogMessage("ZEALOTRY: Conditions not met, not casting");
                            }
                        }
                        else
                        {
                            // Check if this might be a skill we're looking for but not detecting properly
                            var skillName = skill.InternalName.ToLower();
                            if (skillName.Contains("relic") || skillName.Contains("zealot") || skillName.Contains("blessing"))
                            {
                                BetterFollowbotLite.Instance.LogMessage($"AURA BLESSING: Potential aura skill detected - Name: {skill.InternalName}, ID: {skill.Id}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        BetterFollowbotLite.Instance.LogMessage($"AURA BLESSING: Error processing skill {skill.Id}: {e.Message}");
                    }
                }
                else if (Settings.debugMode)
                {
                    BetterFollowbotLite.Instance.LogMessage("AURA BLESSING: Feature disabled");
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

                                    // Check if we don't have the smite buff
                                var hasSmiteBuff = buffs.Exists(x => x.Name == "smite_buff");
                                BetterFollowbotLite.Instance.LogMessage($"SMITE: Has smite buff: {hasSmiteBuff}");

                                if (!hasSmiteBuff)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("SMITE: No smite buff found, looking for targets");

                                    // Find monsters within 250 units of player (smite attack range)
                                    var targetMonster = enemys
                                        .Where(monster =>
                                        {
                                            // Check if monster is within 250 units of player
                                            var distanceToPlayer = Vector3.Distance(playerPosition, monster.Pos);
                                            BetterFollowbotLite.Instance.LogMessage($"SMITE: Monster at distance {distanceToPlayer:F1} from player");
                                            return distanceToPlayer <= 250;
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

                                        // Activate the skill
                                        Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                        SkillInfo.smite.Cooldown = 100;

                                        BetterFollowbotLite.Instance.LogMessage("SMITE: Smite activated successfully");
                                    }
                                    else
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("SMITE: No suitable targets found within range, dashing to leader");

                                        // Dash to leader to get near monsters
                                        if (Settings.autoPilotDashEnabled && (DateTime.Now - autoPilot.lastDashTime).TotalMilliseconds >= 3000 && autoPilot.FollowTarget != null)
                                        {
                                            var leaderPos = autoPilot.FollowTarget.Pos;
                                            var distanceToLeader = Vector3.Distance(playerPosition, leaderPos);

                                            if (distanceToLeader > 50) // Only dash if we're not already close to leader
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