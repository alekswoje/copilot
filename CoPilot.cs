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

namespace CoPilot;

public class CoPilot : BaseSettingsPlugin<CoPilotSettings>
{
    private const int Delay = 45;

    private const int MouseAutoSnapRange = 250;
    internal static CoPilot Instance;
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

            if (Settings.autoQuitHotkeyEnabled && (WinApi.GetAsyncKeyState(Settings.forcedAutoQuit) & 0x8000) != 0)
            {
                Quit();
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


            #region Auto Quit

            if (Settings.autoQuitEnabled)
                try
                {
                    if (player.HPPercentage < (float)Settings.hppQuit / 100||
                        player.MaxES > 0 &&
                        player.ESPercentage < (float)Settings.espQuit / 100)
                        Quit();
                }
                catch (Exception e)
                {
                    // Error handling without logging
                }

            if (Settings.autoQuitGuardian)
                try
                {
                    if (Summons.GetAnimatedGuardianHpp() < (float)Settings.guardianHpp / 100)
                        Quit();
                }
                catch (Exception e)
                {
                    // Error handling without logging
                }

            #endregion

                

            // Do not Cast anything while we are untouchable or Chat is Open
            if (buffs.Exists(x => x.Name == "grace_period") ||
                /*GameController.IngameState.IngameUi.ChatBoxRoot.Parent.Parent.Parent.GetChildAtIndex(3).IsVisible || */ // 3.15 Bugged 
                !GameController.IsForeGroundCache)
                return;
                
            foreach (var skill in skills.Where(skill => skill.IsOnSkillBar && skill.SkillSlotIndex >= 1 && skill.SkillSlotIndex != 2 && skill.CanBeUsed))
            {
                    

                #region Aura Blessing

                if (Settings.auraBlessingEnabled)
                    try
                    {
                        if (SkillInfo.ManageCooldown(SkillInfo.blessing, skill))
                        {
                            //guard statement to check for withering step
                            if (Settings.auraBlessingWitheringStep && buffs.Exists(b => b.Name == SkillInfo.witherStep.BuffName)) return;
                            var cachedSkill = SkillInfo.CachedAuraSkills.Find(s => s.IsBlessing > 0 && s.Id == skill.Id);
                            if (cachedSkill != null && !buffs.Exists(x => x.Name == cachedSkill.BuffName && x.Timer > 0.2))
                                if (MonsterCheck(Settings.auraBlessingRange, Settings.auraBlessingMinAny,
                                        Settings.auraBlessingMinRare, Settings.auraBlessingMinUnique) &&
                                    (player.HPPercentage <=
                                     (float)Settings.auraBlessingHpp / 100 ||
                                     player.MaxES > 0 && player.ESPercentage <
                                     (float)Settings.auraBlessingEsp / 100))
                                    Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
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
                            CoPilot.Instance.LogMessage("SMITE: Smite skill detected");

                            // Custom cooldown check for smite that bypasses GCD since it's a buff skill
                            if (SkillInfo.smite.Cooldown <= 0 &&
                                !(skill.RemainingUses <= 0 && skill.IsOnCooldown))
                            {
                                // Check mana cost
                                if (!skill.Stats.TryGetValue(GameStat.ManaCost, out var manaCost))
                                    manaCost = 0;

                                if (CoPilot.Instance.player.CurMana >= manaCost ||
                                    (CoPilot.Instance.localPlayer.Stats.TryGetValue(GameStat.VirtualEnergyShieldProtectsMana, out var hasEldritchBattery) &&
                                     hasEldritchBattery > 0 && (CoPilot.Instance.player.CurES + CoPilot.Instance.player.CurMana) >= manaCost))
                                {
                                    CoPilot.Instance.LogMessage("SMITE: Cooldown check passed");

                                    // Check if we don't have the smite buff
                                var hasSmiteBuff = buffs.Exists(x => x.Name == "smite_buff");
                                CoPilot.Instance.LogMessage($"SMITE: Has smite buff: {hasSmiteBuff}");

                                if (!hasSmiteBuff)
                                {
                                    CoPilot.Instance.LogMessage("SMITE: No smite buff found, looking for targets");

                                    // Find monsters within 250 units of player (smite attack range)
                                    var targetMonster = enemys
                                        .Where(monster =>
                                        {
                                            // Check if monster is within 250 units of player
                                            var distanceToPlayer = Vector3.Distance(playerPosition, monster.Pos);
                                            CoPilot.Instance.LogMessage($"SMITE: Monster at distance {distanceToPlayer:F1} from player");
                                            return distanceToPlayer <= 250;
                                        })
                                        .OrderBy(monster => Vector3.Distance(playerPosition, monster.Pos)) // Closest first
                                        .FirstOrDefault();

                                    if (targetMonster != null)
                                    {
                                        CoPilot.Instance.LogMessage("SMITE: Found suitable target, activating smite!");

                                        // Move mouse to monster position
                                        var monsterScreenPos = GameController.IngameState.Camera.WorldToScreen(targetMonster.Pos);
                                        Mouse.SetCursorPos(monsterScreenPos);

                                        // Small delay to ensure mouse movement is registered
                                        System.Threading.Thread.Sleep(50);

                                        // Activate the skill
                                        Keyboard.KeyPress(GetSkillInputKey(skill.SkillSlotIndex));
                                        SkillInfo.smite.Cooldown = 100;

                                        CoPilot.Instance.LogMessage("SMITE: Smite activated successfully");
                                    }
                                    else
                                    {
                                        CoPilot.Instance.LogMessage("SMITE: No suitable targets found within range, dashing to leader");

                                        // Dash to leader to get near monsters
                                        if (Settings.autoPilotDashEnabled && (DateTime.Now - autoPilot.lastDashTime).TotalMilliseconds >= 3000 && autoPilot.followTarget != null)
                                        {
                                            var leaderPos = autoPilot.followTarget.Pos;
                                            var distanceToLeader = Vector3.Distance(playerPosition, leaderPos);

                                            if (distanceToLeader > 50) // Only dash if we're not already close to leader
                                            {
                                                CoPilot.Instance.LogMessage($"SMITE: Dashing to leader - Distance: {distanceToLeader:F1}");

                                                // Position mouse towards leader
                                                var leaderScreenPos = GameController.IngameState.Camera.WorldToScreen(leaderPos);
                                                Mouse.SetCursorPos(leaderScreenPos);

                                                // Small delay to ensure mouse movement is registered
                                                System.Threading.Thread.Sleep(50);

                                                // Execute dash
                                                Keyboard.KeyPress(Settings.autoPilotDashKey);
                                                autoPilot.lastDashTime = DateTime.Now;

                                                CoPilot.Instance.LogMessage("SMITE: Dash to leader executed");
                                            }
                                            else
                                            {
                                                CoPilot.Instance.LogMessage("SMITE: Already close to leader, skipping dash");
                                            }
                                        }
                                        else
                                        {
                                            CoPilot.Instance.LogMessage("SMITE: Dash not available or not enabled");
                                        }
                                    }
                                }
                                    else
                                    {
                                        CoPilot.Instance.LogMessage("SMITE: Already have smite buff, skipping");
                                    }
                                }
                                else
                                {
                                    CoPilot.Instance.LogMessage("SMITE: Not enough mana for smite");
                                }
                            }
                            else
                            {
                                CoPilot.Instance.LogMessage("SMITE: Cooldown check failed");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Error handling without logging
                    }
                else
                {
                    CoPilot.Instance.LogMessage("SMITE: Smite is not enabled");
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