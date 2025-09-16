using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace CoPilot;

public class AutoPilot
{
    // Most Logic taken from Alpha Plugin
    private Coroutine autoPilotCoroutine;
    private readonly Random random = new Random();

    private Vector3 lastTargetPosition;
    private Vector3 lastPlayerPosition;
    private Entity followTarget;

    private bool hasUsedWp;
    private List<TaskNode> tasks = new List<TaskNode>();
    private DateTime lastDashTime = DateTime.MinValue; // Track last dash time for cooldown
    private bool instantPathOptimization = false; // Flag for instant response when path efficiency is detected
    private DateTime lastPathClearTime = DateTime.MinValue; // Track last path clear to prevent spam

    private int numRows, numCols;
    private byte[,] tiles;

    /// <summary>
    /// Checks if the cursor is pointing roughly towards the target direction in screen space
    /// </summary>
    private bool IsCursorPointingTowardsTarget(Vector3 targetPosition)
    {
        try
        {
            // Get the current mouse position in screen coordinates
            var mouseScreenPos = CoPilot.Instance.GetMousePosition();

            // Get the player's screen position
            var playerScreenPos = Helper.WorldToValidScreenPosition(CoPilot.Instance.playerPosition);

            // Get the target's screen position
            var targetScreenPos = Helper.WorldToValidScreenPosition(targetPosition);

            // Calculate the direction from player to target in screen space
            var playerToTarget = targetScreenPos - playerScreenPos;
            if (playerToTarget.Length() < 20) // Target is too close in screen space
                return true; // Consider it pointing towards target

            playerToTarget.Normalize();

            // Calculate the direction from player to cursor in screen space
            var playerToCursor = mouseScreenPos - playerScreenPos;
            if (playerToCursor.Length() < 30) // Cursor is too close to player in screen space
                return false; // Can't determine direction reliably

            playerToCursor.Normalize();

            // Calculate the angle between the two directions
            var dotProduct = Vector2.Dot(playerToTarget, playerToCursor);
            var angle = Math.Acos(Math.Max(-1, Math.Min(1, dotProduct))) * (180.0 / Math.PI);

            // Allow up to 60 degrees difference (cursor should be roughly pointing towards target)
            return angle <= 60.0;
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError($"Cursor direction check error: {e}");
            return false; // Default to false if we can't determine direction
        }
    }

    /// <summary>
    /// Checks if enough time has passed since the last dash (3 second cooldown)
    /// </summary>
    private bool CanDash()
    {
        return (DateTime.Now - lastDashTime).TotalMilliseconds >= 3000; // Increased from 1000ms to 3000ms (3 seconds)
    }

    /// <summary>
    /// Checks if the player has moved significantly and we should clear the current path for better responsiveness
    /// More aggressive for 180-degree turns
    /// </summary>
    private bool ShouldClearPathForResponsiveness()
    {
        return ShouldClearPathForResponsiveness(false);
    }

    /// <summary>
    /// Checks if the player has moved significantly and we should clear the current path for better responsiveness
    /// More aggressive for 180-degree turns
    /// </summary>
    private bool ShouldClearPathForResponsiveness(bool isOverrideCheck)
    {
        try
        {
            // For override checks (after click), be more aggressive with timing
            int rateLimitMs = isOverrideCheck ? 100 : 300; // Override checks can happen more frequently
            if ((DateTime.Now - lastPathClearTime).TotalMilliseconds < rateLimitMs)
                return false;

            // Need a follow target to check responsiveness
            if (followTarget == null)
                return false;

            // Need existing tasks to clear
            if (tasks.Count == 0)
                return false;

            // Calculate how much the player has moved since last update
            var playerMovement = Vector3.Distance(CoPilot.Instance.playerPosition, lastPlayerPosition);
            
            // More aggressive: If player moved more than 30 units, clear path for responsiveness
            if (playerMovement > 30f)
            {
                CoPilot.Instance.LogMessage($"RESPONSIVENESS: Player moved {playerMovement:F1} units, clearing path for better tracking");
                lastPathClearTime = DateTime.Now;
                return true;
            }

            // Check for 180-degree turn detection - VERY AGGRESSIVE
            if (tasks.Count > 0 && tasks[0].WorldPosition != null)
            {
                Vector3 botPos = CoPilot.Instance.localPlayer?.Pos ?? CoPilot.Instance.playerPosition;
                Vector3 playerPos = CoPilot.Instance.playerPosition;
                Vector3 currentTaskTarget = tasks[0].WorldPosition;

                // Calculate direction from bot to current task
                Vector3 botToTask = currentTaskTarget - botPos;
                // Calculate direction from bot to player
                Vector3 botToPlayer = playerPos - botPos;

                if (botToTask.Length() > 10f && botToPlayer.Length() > 10f)
                {
                    botToTask = Vector3.Normalize(botToTask);
                    botToPlayer = Vector3.Normalize(botToPlayer);

                    // Calculate dot product - if negative, player is behind the current task direction
                    float dotProduct = Vector3.Dot(botToTask, botToPlayer);
                    
                    // VERY AGGRESSIVE: If player is more than 60 degrees away from task direction
                    if (dotProduct < 0.5f) // 60 degrees
                    {
                        CoPilot.Instance.LogMessage($"180 DEGREE DETECTION: Player direction conflicts with task (dot={dotProduct:F2}), clearing path");
                        lastPathClearTime = DateTime.Now;
                        return true;
                    }
                }
            }

            // Also check if we're following an old position that's now far from current player position
            var distanceToCurrentPlayer = Vector3.Distance(CoPilot.Instance.localPlayer?.Pos ?? CoPilot.Instance.playerPosition, followTarget.Pos);
            if (distanceToCurrentPlayer > 80f) // More aggressive - reduced from 100f
            {
                CoPilot.Instance.LogMessage($"RESPONSIVENESS: Target {distanceToCurrentPlayer:F1} units away, clearing path for better tracking");
                lastPathClearTime = DateTime.Now;
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError($"Responsiveness check error: {e}");
            return false;
        }
    }

    /// <summary>
    /// Calculates the efficiency of the current path compared to moving directly to player
    /// Returns efficiency ratio: direct_distance / path_distance
    /// Lower values mean the direct path is much shorter (more efficient)
    /// </summary>
    private float CalculatePathEfficiency()
    {
        try
        {
            if (tasks.Count == 0 || followTarget == null)
                return 1.0f; // No path or no target, consider efficient

            // Check efficiency even for single tasks if they're movement tasks
            bool hasMovementTask = tasks.Any(t => t.Type == TaskNodeType.Movement);

            // Calculate direct distance from bot to player
            float directDistance = Vector3.Distance(CoPilot.Instance.localPlayer?.Pos ?? CoPilot.Instance.playerPosition, followTarget?.Pos ?? CoPilot.Instance.playerPosition);

            // If we're already very close to the player, don't bother with efficiency calculations
            if (directDistance < 30f) // Reduced from 50f
                return 1.0f;

            // Calculate distance along current path
            float pathDistance = 0f;
            Vector3 currentPos = CoPilot.Instance.localPlayer?.Pos ?? CoPilot.Instance.playerPosition;

            // Add distance to each path node
            foreach (var task in tasks)
            {
                if (task.WorldPosition != null)
                {
                    pathDistance += Vector3.Distance(currentPos, task.WorldPosition);
                    currentPos = task.WorldPosition;
                }
            }

            // If no valid path distance, return 1.0 (neutral)
            if (pathDistance <= 0)
                return 1.0f;

            // If the path is very short, it's already efficient
            if (pathDistance < 50f) // Reduced from 100f
                return 1.0f;

            // Calculate efficiency ratio
            float efficiency = directDistance / pathDistance;

            // More detailed logging for debugging
            CoPilot.Instance.LogMessage($"Path efficiency: Direct={directDistance:F1}, Path={pathDistance:F1}, Ratio={efficiency:F2}, Tasks={tasks.Count}");
            if (efficiency < 0.8f) // Log when efficiency is getting low
            {
                CoPilot.Instance.LogMessage($"LOW EFFICIENCY DETECTED: {efficiency:F2} - Direct path is {(1f/efficiency):F1}x shorter!");
            }
            return efficiency;
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError($"Path efficiency calculation error: {e}");
            return 1.0f; // Default to neutral on error
        }
    }

    /// <summary>
    /// Checks if the current path is inefficient and should be abandoned
    /// Returns true if path should be cleared for direct movement to player
    /// </summary>
    private bool ShouldAbandonPathForEfficiency()
    {
        try
        {
            // Check even single tasks if they're movement tasks and we have a follow target
            bool shouldCheckEfficiency = tasks.Count >= 1 && followTarget != null;

            if (!shouldCheckEfficiency)
            {
                CoPilot.Instance.LogMessage($"Path efficiency check skipped: Tasks={tasks.Count}, FollowTarget={followTarget != null}");
                return false;
            }

            float efficiency = CalculatePathEfficiency();

            // If direct path is much shorter (more than 20% shorter) than following current path - VERY AGGRESSIVE
            if (efficiency < 0.8f) // Changed from 0.7f to 0.8f for even more aggressive clearing
            {
                CoPilot.Instance.LogMessage($"PATH ABANDONED FOR EFFICIENCY: {efficiency:F2} < 0.8 (Direct path {(1f/efficiency):F1}x shorter)");
                return true;
            }

            // Also check if player is now behind us relative to path direction (more aggressive check)
            if (tasks.Count >= 1 && tasks[0].WorldPosition != null)
            {
                Vector3 botPos = CoPilot.Instance.localPlayer?.Pos ?? CoPilot.Instance.playerPosition;
                Vector3 playerPos = CoPilot.Instance.playerPosition;
                Vector3 pathTarget = tasks[0].WorldPosition;

                // Calculate vectors
                Vector3 botToPath = pathTarget - botPos;
                Vector3 botToPlayer = playerPos - botPos;

                // Normalize vectors for dot product calculation
                if (botToPath.Length() > 0 && botToPlayer.Length() > 0)
                {
                    botToPath = Vector3.Normalize(botToPath);
                    botToPlayer = Vector3.Normalize(botToPlayer);

                            // If player is behind us on the path (negative dot product) - VERY SENSITIVE
                            float dotProduct = Vector3.Dot(botToPath, botToPlayer);
                            if (dotProduct < -0.1f) // Changed from -0.3f to -0.1f (95 degrees) - even more sensitive
                    {
                        CoPilot.Instance.LogMessage($"Path abandoned: Player behind bot (dot={dotProduct:F2})");
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError($"Path abandonment check error: {e}");
            return false;
        }
    }

    /// <summary>
    /// Clears all pathfinding values. Used on area transitions primarily.
    /// </summary>
    private void ResetPathing()
    {
        tasks = new List<TaskNode>();
        followTarget = null;
        lastTargetPosition = Vector3.Zero;
        lastPlayerPosition = Vector3.Zero;
        hasUsedWp = false;
        lastDashTime = DateTime.MinValue; // Reset dash cooldown on area change
        instantPathOptimization = false; // Reset instant optimization flag
        lastPathClearTime = DateTime.MinValue; // Reset responsiveness tracking
    }

    /// <summary>
    /// Clears path due to efficiency optimization (not area change)
    /// </summary>
    private void ClearPathForEfficiency()
    {
        tasks.Clear();
        hasUsedWp = false; // Allow waypoint usage again
        // Note: Don't reset dash cooldown for efficiency clears
        // instantPathOptimization flag is managed separately
    }

    private PartyElementWindow GetLeaderPartyElement()
    {
        try
        {
            foreach (var partyElementWindow in PartyElements.GetPlayerInfoElementList())
            {
                if (string.Equals(partyElementWindow?.PlayerName?.ToLower(), CoPilot.Instance.Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase))
                {
                    return partyElementWindow;
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private LabelOnGround GetBestPortalLabel(PartyElementWindow leaderPartyElement)
    {
        try
        {
            if (leaderPartyElement == null)
                return null;

            var currentZoneName = CoPilot.Instance.GameController?.Area.CurrentArea.DisplayName;
            // Look for portals when leader is in different zone, or when in hideout, or in high level areas
            if(!leaderPartyElement.ZoneName.Equals(currentZoneName) || (bool)CoPilot.Instance?.GameController?.Area?.CurrentArea?.IsHideout || CoPilot.Instance.GameController?.Area?.CurrentArea?.RealLevel >= 68) // TODO: or is chamber of sins a7 or is epilogue
            {
                var portalLabels =
                    CoPilot.Instance.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels.Where(x =>
                            x != null && x.IsVisible && x.Label != null && x.Label.IsValid && x.Label.IsVisible && x.ItemOnGround != null && 
                            (x.ItemOnGround.Metadata.ToLower().Contains("areatransition") || x.ItemOnGround.Metadata.ToLower().Contains("portal") ))
                        .OrderBy(x => Vector3.Distance(lastTargetPosition, x.ItemOnGround.Pos)).ToList();


                return CoPilot.Instance?.GameController?.Area?.CurrentArea?.IsHideout != null && (bool)CoPilot.Instance.GameController?.Area?.CurrentArea?.IsHideout
                    ? portalLabels?[random.Next(portalLabels.Count)]
                    : portalLabels?.FirstOrDefault();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
    private Vector2 GetTpButton(PartyElementWindow leaderPartyElement)
    {
        try
        {
            if (leaderPartyElement == null)
                return Vector2.Zero;

            var windowOffset = CoPilot.Instance.GameController.Window.GetWindowRectangle().TopLeft;
            var elemCenter = (Vector2) leaderPartyElement?.TpButton?.GetClientRectCache.Center;
            var finalPos = new Vector2(elemCenter.X + windowOffset.X, elemCenter.Y + windowOffset.Y);

            return finalPos;
        }
        catch
        {
            return Vector2.Zero;
        }
    }
    private Element GetTpConfirmation()
    {
        try
        {
            var ui = CoPilot.Instance.GameController?.IngameState?.IngameUi?.PopUpWindow;

            if (ui.Children[0].Children[0].Children[0].Text.Equals("Are you sure you want to teleport to this player's location?"))
                return ui.Children[0].Children[0].Children[3].Children[0];

            return null;
        }
        catch
        {
            return null;
        }
    }
    public void AreaChange()
    {
        ResetPathing();

        var terrain = CoPilot.Instance.GameController.IngameState.Data.Terrain;
        var terrainBytes = CoPilot.Instance.GameController.Memory.ReadBytes(terrain.LayerMelee.First, terrain.LayerMelee.Size);
        numCols = (int)(terrain.NumCols - 1) * 23;
        numRows = (int)(terrain.NumRows - 1) * 23;
        if ((numCols & 1) > 0)
            numCols++;

        tiles = new byte[numCols, numRows];
        var dataIndex = 0;
        for (var y = 0; y < numRows; y++)
        {
            for (var x = 0; x < numCols; x += 2)
            {
                var b = terrainBytes[dataIndex + (x >> 1)];
                tiles[x, y] = (byte)((b & 0xf) > 0 ? 1 : 255);
                tiles[x+1, y] = (byte)((b >> 4) > 0 ? 1 : 255);
            }
            dataIndex += terrain.BytesPerRow;
        }

        terrainBytes = CoPilot.Instance.GameController.Memory.ReadBytes(terrain.LayerRanged.First, terrain.LayerRanged.Size);
        numCols = (int)(terrain.NumCols - 1) * 23;
        numRows = (int)(terrain.NumRows - 1) * 23;
        if ((numCols & 1) > 0)
            numCols++;
        dataIndex = 0;
        for (var y = 0; y < numRows; y++)
        {
            for (var x = 0; x < numCols; x += 2)
            {
                var b = terrainBytes[dataIndex + (x >> 1)];

                var current = tiles[x, y];
                if(current == 255)
                    tiles[x, y] = (byte)((b & 0xf) > 3 ? 2 : 255);
                current = tiles[x+1, y];
                if (current == 255)
                    tiles[x + 1, y] = (byte)((b >> 4) > 3 ? 2 : 255);
            }
            dataIndex += terrain.BytesPerRow;
        }
    }

    public void StartCoroutine()
    {
        CoPilot.Instance.LogMessage("AutoPilot: Starting new coroutine");
        autoPilotCoroutine = new Coroutine(AutoPilotLogic(), CoPilot.Instance, "AutoPilot");
        Core.ParallelRunner.Run(autoPilotCoroutine);
        CoPilot.Instance.LogMessage("AutoPilot: Coroutine started successfully");
    }
    private IEnumerator MouseoverItem(Entity item)
    {
        var uiLoot = CoPilot.Instance.GameController.IngameState.IngameUi.ItemsOnGroundLabels.FirstOrDefault(I => I.IsVisible && I.ItemOnGround.Id == item.Id);
        if (uiLoot == null) yield return null;
        var clickPos = uiLoot?.Label?.GetClientRect().Center;
        if (clickPos != null)
        {
            Mouse.SetCursorPos(new Vector2(
                clickPos.Value.X + random.Next(-15, 15),
                clickPos.Value.Y + random.Next(-10, 10)));
        }

        yield return new WaitTime(30 + random.Next(CoPilot.Instance.Settings.autoPilotInputFrequency));
    }
    private IEnumerator AutoPilotLogic()
    {
        CoPilot.Instance.LogMessage("AutoPilotLogic: Coroutine started");
        while (true)
        {
            if (!CoPilot.Instance.Settings.Enable.Value || !CoPilot.Instance.Settings.autoPilotEnabled.Value || CoPilot.Instance.localPlayer == null || !CoPilot.Instance.localPlayer.IsAlive ||
                !CoPilot.Instance.GameController.IsForeGroundCache || MenuWindow.IsOpened || CoPilot.Instance.GameController.IsLoading || !CoPilot.Instance.GameController.InGame)
            {
                yield return new WaitTime(100);
                continue;
            }

            // Only execute input tasks here - decision making moved to Render method
            if (tasks?.Count > 0)
            {
                TaskNode currentTask = null;
                bool taskAccessError = false;

                try
                {
                    currentTask = tasks.First();
                }
                catch (Exception e)
                {
                    CoPilot.Instance.LogError($"Task access error: {e}");
                    taskAccessError = true;
                }

                if (taskAccessError)
                {
                    yield return new WaitTime(50);
                    continue;
                }

                if (currentTask?.WorldPosition == null)
                {
                    CoPilot.Instance.LogError("Coroutine: Invalid task with null WorldPosition, removing");
                    tasks.RemoveAt(0);
                    yield return new WaitTime(50);
                    continue;
                }

                var taskDistance = Vector3.Distance(CoPilot.Instance.playerPosition, currentTask.WorldPosition);
                var playerDistanceMoved = Vector3.Distance(CoPilot.Instance.playerPosition, lastPlayerPosition);

                CoPilot.Instance.LogMessage($"Coroutine executing task: {currentTask.Type}, Task count: {tasks.Count}, Distance: {taskDistance:F1}");

                // Check if we should clear path for better responsiveness to player movement
                if (ShouldClearPathForResponsiveness())
                {
                    CoPilot.Instance.LogMessage("RESPONSIVENESS: Clearing path for better player tracking");
                    instantPathOptimization = true; // Enable instant mode for immediate response
                    ClearPathForEfficiency(); // Clear all tasks and reset related state
                    
                    // FORCE IMMEDIATE PATH CREATION - Don't wait for UpdateAutoPilotLogic
                    if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                    {
                        var instantDistanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                        CoPilot.Instance.LogMessage($"RESPONSIVENESS: Creating immediate direct path - Distance: {instantDistanceToLeader:F1}");
                        
                        if (instantDistanceToLeader > 1000 && CoPilot.Instance.Settings.autoPilotDashEnabled) // Increased from 700 to 1000
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, 0, TaskNodeType.Dash));
                        }
                        else
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                        }
                    }
                    
                    yield return null; // INSTANT: No delay, immediate path recalculation
                    continue; // Skip current task processing, will recalculate path immediately
                }

                // Check if current path is inefficient and should be abandoned - INSTANT RESPONSE
                if (ShouldAbandonPathForEfficiency())
                {
                    CoPilot.Instance.LogMessage("INSTANT PATH OPTIMIZATION: Clearing inefficient path for direct movement");
                    instantPathOptimization = true; // Enable instant mode for immediate response
                    ClearPathForEfficiency(); // Clear all tasks and reset related state
                    
                    // FORCE IMMEDIATE PATH CREATION - Don't wait for UpdateAutoPilotLogic
                    if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                    {
                        var instantDistanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                        CoPilot.Instance.LogMessage($"INSTANT PATH OPTIMIZATION: Creating immediate direct path - Distance: {instantDistanceToLeader:F1}");
                        
                        if (instantDistanceToLeader > 1000 && CoPilot.Instance.Settings.autoPilotDashEnabled) // Increased from 700 to 1000
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, 0, TaskNodeType.Dash));
                        }
                        else
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                        }
                    }
                    
                    yield return null; // INSTANT: No delay, immediate path recalculation
                    continue; // Skip current task processing, will recalculate path immediately
                }

                //We are using a same map transition and have moved significnatly since last tick. Mark the transition task as done.
                if (currentTask.Type == TaskNodeType.Transition &&
                    playerDistanceMoved >= CoPilot.Instance.Settings.autoPilotClearPathDistance.Value)
                {
                    tasks.RemoveAt(0);
                    lastPlayerPosition = CoPilot.Instance.playerPosition;
                    yield return null;
                    continue;
                }

                // Variables to track state outside try-catch blocks
                bool shouldDashToLeader = false;
                bool shouldTerrainDash = false;
                Vector2 movementScreenPos = Vector2.Zero;
                bool screenPosError = false;
                bool keyDownError = false;
                bool keyUpError = false;
                bool taskExecutionError = false;

                // Action flags for different task types
                bool shouldLootAndContinue = false;
                bool shouldTransitionAndContinue = false;
                bool shouldClaimWaypointAndContinue = false;
                bool shouldDashAndContinue = false;
                bool shouldTeleportConfirmAndContinue = false;
                bool shouldTeleportButtonAndContinue = false;
                bool shouldMovementContinue = false;

                // Loot-related variables
                Entity questLoot = null;
                Targetable targetInfo = null;

                // Transition-related variables
                Vector2 transitionPos = Vector2.Zero;

                // Waypoint-related variables
                Vector2 waypointScreenPos = Vector2.Zero;

                try
                {
                    switch (currentTask.Type)
                    {
                        case TaskNodeType.Movement:
                            CoPilot.Instance.LogMessage($"Movement task executing - Distance to target: {taskDistance:F1}, Required: {CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value * 1.5:F1}");

                            // Check for distance-based dashing to keep up with leader
                            if (CoPilot.Instance.Settings.autoPilotDashEnabled && followTarget != null && followTarget.Pos != null && CanDash())
                            {
                                try
                                {
                                    var distanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                                    CoPilot.Instance.LogMessage($"Movement task: Checking dash - Distance to leader: {distanceToLeader:F1}, Dash enabled: {CoPilot.Instance.Settings.autoPilotDashEnabled}, Threshold: 700, Can dash: {CanDash()}");
                                    if (distanceToLeader > 700 && IsCursorPointingTowardsTarget(followTarget.Pos)) // Dash if more than 700 units away and cursor is pointing towards leader
                                    {
                                        CoPilot.Instance.LogMessage($"Movement task: Dashing to leader - Distance: {distanceToLeader:F1}, Cursor direction valid");
                                        shouldDashToLeader = true;
                                    }
                                    else if (distanceToLeader > 700 && !IsCursorPointingTowardsTarget(followTarget.Pos))
                                    {
                                        CoPilot.Instance.LogMessage($"Movement task: Not dashing - Distance: {distanceToLeader:F1} but cursor not pointing towards target");
                                    }
                                    else
                                    {
                                        CoPilot.Instance.LogMessage($"Movement task: Not dashing - Distance {distanceToLeader:F1} <= 700");
                                    }
                                }
                                catch (Exception e)
                                {
                                    CoPilot.Instance.LogError($"Movement task: Dash calculation error: {e}");
                                }
                            }
                            else
                            {
                                CoPilot.Instance.LogMessage($"Movement task: Dash check skipped - Dash enabled: {CoPilot.Instance.Settings.autoPilotDashEnabled}, Follow target: {followTarget != null}, Follow target pos: {followTarget?.Pos != null}");
                            }

                            // Check for terrain-based dashing
                            if (CoPilot.Instance.Settings.autoPilotDashEnabled && CanDash())
                            {
                                CoPilot.Instance.LogMessage("Movement task: Checking terrain dash");
                                if (CheckDashTerrain(currentTask.WorldPosition.WorldToGrid()) && IsCursorPointingTowardsTarget(currentTask.WorldPosition))
                                {
                                    CoPilot.Instance.LogMessage("Movement task: Terrain dash executed - Cursor direction valid");
                                    shouldTerrainDash = true;
                                }
                                else if (CheckDashTerrain(currentTask.WorldPosition.WorldToGrid()) && !IsCursorPointingTowardsTarget(currentTask.WorldPosition))
                                {
                                    CoPilot.Instance.LogMessage("Movement task: Terrain dash blocked - Cursor not pointing towards target");
                                }
                                else
                                {
                                    CoPilot.Instance.LogMessage("Movement task: No terrain dash needed");
                                }
                            }

                            // Skip movement logic if dashing
                            if (!shouldDashToLeader && !shouldTerrainDash)
                            {
                                CoPilot.Instance.LogMessage($"Movement task: Moving to {currentTask.WorldPosition}");

                                try
                                {
                                    movementScreenPos = Helper.WorldToValidScreenPosition(currentTask.WorldPosition);
                                    CoPilot.Instance.LogMessage($"Movement task: Screen position: {movementScreenPos}");
                                }
                                catch (Exception e)
                                {
                                    CoPilot.Instance.LogError($"Movement task: Screen position calculation error: {e}");
                                    screenPosError = true;
                                }

                                if (!screenPosError)
                                {
                                    try
                                    {
                                        Input.KeyDown(CoPilot.Instance.Settings.autoPilotMoveKey);
                                        CoPilot.Instance.LogMessage("Movement task: Move key down pressed, waiting");
                                    }
                                    catch (Exception e)
                                    {
                                        CoPilot.Instance.LogError($"Movement task: KeyDown error: {e}");
                                        keyDownError = true;
                                    }

                                    try
                                    {
                                        Input.KeyUp(CoPilot.Instance.Settings.autoPilotMoveKey);
                                        CoPilot.Instance.LogMessage("Movement task: Move key released");
                                    }
                                    catch (Exception e)
                                    {
                                        CoPilot.Instance.LogError($"Movement task: KeyUp error: {e}");
                                        keyUpError = true;
                                    }

                                    //Within bounding range. Task is complete
                                    //Note: Was getting stuck on close objects... testing hacky fix.
                                    if (taskDistance <= CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value * 1.5)
                                    {
                                        CoPilot.Instance.LogMessage($"Movement task completed - Distance: {taskDistance:F1}");
                                        tasks.RemoveAt(0);
                                        lastPlayerPosition = CoPilot.Instance.playerPosition;
                                    }
                                    else
                                    {
                                        // Timeout mechanism - if we've been trying to reach this task for too long, give up
                                        currentTask.AttemptCount++;
                                        if (currentTask.AttemptCount > 10) // 10 attempts = ~5 seconds
                                        {
                                            CoPilot.Instance.LogMessage($"Movement task timeout - Distance: {taskDistance:F1}, Attempts: {currentTask.AttemptCount}");
                                            tasks.RemoveAt(0);
                                            lastPlayerPosition = CoPilot.Instance.playerPosition;
                                        }
                                    }
                                    shouldMovementContinue = true;
                                }
                            }
                            break;
                        case TaskNodeType.Loot:
                        {
                            currentTask.AttemptCount++;
                            questLoot = GetQuestItem();
                            if (questLoot == null
                                || currentTask.AttemptCount > 2
                                || Vector3.Distance(CoPilot.Instance.playerPosition, questLoot.Pos) >=
                                CoPilot.Instance.Settings.autoPilotClearPathDistance.Value)
                            {
                                tasks.RemoveAt(0);
                                shouldLootAndContinue = true;
                            }
                            else
                            {
                                Input.KeyUp(CoPilot.Instance.Settings.autoPilotMoveKey);
                                if (questLoot != null)
                                {
                                    targetInfo = questLoot.GetComponent<Targetable>();
                                }
                            }
                            break;
                        }
                        case TaskNodeType.Transition:
                        {
                            //Click the transition
                            Input.KeyUp(CoPilot.Instance.Settings.autoPilotMoveKey);
                            transitionPos = new Vector2(currentTask.LabelOnGround.Label.GetClientRect().Center.X, currentTask.LabelOnGround.Label.GetClientRect().Center.Y);

                            currentTask.AttemptCount++;
                            if (currentTask.AttemptCount > 6)
                                tasks.RemoveAt(0);
                            shouldTransitionAndContinue = true;
                            break;
                        }

                        case TaskNodeType.ClaimWaypoint:
                        {
                            if (Vector3.Distance(CoPilot.Instance.playerPosition, currentTask.WorldPosition) > 150)
                            {
                                waypointScreenPos = Helper.WorldToValidScreenPosition(currentTask.WorldPosition);
                                Input.KeyUp(CoPilot.Instance.Settings.autoPilotMoveKey);
                            }
                            currentTask.AttemptCount++;
                            if (currentTask.AttemptCount > 3)
                                tasks.RemoveAt(0);
                            shouldClaimWaypointAndContinue = true;
                            break;
                        }

                         case TaskNodeType.Dash:
                         {
                             CoPilot.Instance.LogMessage($"Executing Dash task - Target: {currentTask.WorldPosition}, Distance: {Vector3.Distance(CoPilot.Instance.playerPosition, currentTask.WorldPosition):F1}");
                             if (CanDash() && IsCursorPointingTowardsTarget(currentTask.WorldPosition))
                             {
                                 tasks.RemoveAt(0);
                                 lastPlayerPosition = CoPilot.Instance.playerPosition;
                                 CoPilot.Instance.LogMessage("Dash task completed successfully - Cursor direction valid");
                                 shouldDashAndContinue = true;
                             }
                             else if (!CanDash())
                             {
                                 CoPilot.Instance.LogMessage("Dash task blocked - Cooldown active");
                             }
                             else if (!IsCursorPointingTowardsTarget(currentTask.WorldPosition))
                             {
                                 CoPilot.Instance.LogMessage("Dash task blocked - Cursor not pointing towards target");
                             }
                             break;
                         }

                        case TaskNodeType.TeleportConfirm:
                        {
                            tasks.RemoveAt(0);
                            shouldTeleportConfirmAndContinue = true;
                            break;
                        }

                        case TaskNodeType.TeleportButton:
                        {
                            tasks.RemoveAt(0);
                            shouldTeleportButtonAndContinue = true;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    CoPilot.Instance.LogError($"Task execution error: {e}");
                    taskExecutionError = true;
                }

                // Handle error cleanup
                if (taskExecutionError)
                {
                    // Remove the problematic task and continue
                    if (tasks.Count > 0)
                    {
                        tasks.RemoveAt(0);
                    }
                }
                // Execute actions outside try-catch blocks
                else
                {
                    if (shouldDashToLeader)
                    {
                        yield return Mouse.SetCursorPosHuman(Helper.WorldToValidScreenPosition(followTarget.Pos));
                        CoPilot.Instance.LogMessage("Movement task: Dash mouse positioned, pressing key");
                        if (instantPathOptimization)
                        {
                            // INSTANT MODE: Skip delays for immediate path correction
                            CoPilot.Instance.LogMessage("INSTANT PATH OPTIMIZATION: Dash with no delays");
                            Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
                            lastDashTime = DateTime.Now; // Record dash time for cooldown
                            instantPathOptimization = false; // Reset flag after use
                        }
                        else
                        {
                            // Normal delays
                            yield return new WaitTime(random.Next(25) + 30);
                            Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
                            lastDashTime = DateTime.Now; // Record dash time for cooldown
                            yield return new WaitTime(random.Next(25) + 30);
                        }
                        yield return null;
                        continue;
                    }

                    if (shouldTerrainDash)
                    {
                        lastDashTime = DateTime.Now; // Record dash time for cooldown (CheckDashTerrain already performed the dash)
                        yield return null;
                        continue;
                    }

                    if (screenPosError)
                    {
                        yield return new WaitTime(50);
                        continue;
                    }

                    if (!screenPosError && currentTask.Type == TaskNodeType.Movement)
                    {
                        // LAST CHANCE CHECK: Before executing movement, check if player has turned around
                        if (ShouldClearPathForResponsiveness())
                        {
                            CoPilot.Instance.LogMessage("LAST CHANCE 180 CHECK: Player direction changed before movement execution, aborting current task");
                            ClearPathForEfficiency();
                            yield return null; // Skip this movement and recalculate
                            continue;
                        }

                        CoPilot.Instance.LogMessage("Movement task: Mouse positioned, pressing move key down");
                        CoPilot.Instance.LogMessage($"Movement task: Move key: {CoPilot.Instance.Settings.autoPilotMoveKey}");
                        yield return Mouse.SetCursorPosHuman(movementScreenPos);
                        
                        // IMMEDIATE OVERRIDE CHECK: After clicking, check if we need to override with new position
                        if (ShouldClearPathForResponsiveness(true)) // Use aggressive override timing
                        {
                            CoPilot.Instance.LogMessage("IMMEDIATE OVERRIDE: 180 detected after click - overriding with new position!");
                            ClearPathForEfficiency();
                            
                            // INSTANT OVERRIDE: Click the correct position immediately to override old movement
                            if (followTarget?.Pos != null)
                            {
                                var correctScreenPos = Helper.WorldToValidScreenPosition(followTarget.Pos);
                                yield return Mouse.SetCursorPosHuman(correctScreenPos);
                                CoPilot.Instance.LogMessage("MOVEMENT OVERRIDE: Clicked correct position to override old movement");
                            }
                            yield return null;
                            continue;
                        }
                        
                        if (instantPathOptimization)
                        {
                            // INSTANT MODE: Skip delays for immediate path correction
                            CoPilot.Instance.LogMessage("INSTANT PATH OPTIMIZATION: Movement with no delays");
                            instantPathOptimization = false; // Reset flag after use
                        }
                        else
                        {
                            // Normal delays
                            yield return new WaitTime(random.Next(25) + 30);
                            yield return new WaitTime(random.Next(25) + 30);
                        }
                        yield return null;
                        continue;
                    }

                    if (shouldLootAndContinue)
                    {
                        yield return null;
                        continue;
                    }

                    if (currentTask.Type == TaskNodeType.Loot && questLoot != null)
                    {
                        yield return new WaitTime(CoPilot.Instance.Settings.autoPilotInputFrequency);
                        if (targetInfo != null)
                        {
                            switch (targetInfo.isTargeted)
                            {
                                case false:
                                    yield return MouseoverItem(questLoot);
                                    break;
                                case true:
                                    yield return Mouse.LeftClick();
                                    yield return new WaitTime(1000);
                                    break;
                            }
                        }
                    }

                    if (shouldTransitionAndContinue)
                    {
                        yield return new WaitTime(60);
                        yield return Mouse.SetCursorPosAndLeftClickHuman(transitionPos, 100);
                        yield return new WaitTime(300);
                        yield return null;
                        continue;
                    }

                    if (shouldClaimWaypointAndContinue)
                    {
                        if (Vector3.Distance(CoPilot.Instance.playerPosition, currentTask.WorldPosition) > 150)
                        {
                            yield return new WaitTime(CoPilot.Instance.Settings.autoPilotInputFrequency);
                            yield return Mouse.SetCursorPosAndLeftClickHuman(waypointScreenPos, 100);
                            yield return new WaitTime(1000);
                        }
                        yield return null;
                        continue;
                    }

                    if (shouldDashAndContinue)
                    {
                        // LAST CHANCE CHECK: Before executing dash, check if player has turned around
                        if (ShouldClearPathForResponsiveness())
                        {
                            CoPilot.Instance.LogMessage("LAST CHANCE 180 CHECK: Player direction changed before dash execution, aborting current task");
                            ClearPathForEfficiency();
                            yield return null; // Skip this dash and recalculate
                            continue;
                        }

                        yield return Mouse.SetCursorPosHuman(Helper.WorldToValidScreenPosition(currentTask.WorldPosition));
                        CoPilot.Instance.LogMessage("Dash: Mouse positioned, pressing dash key");
                        
                        // IMMEDIATE OVERRIDE CHECK: After positioning cursor, check if we need to override
                        if (ShouldClearPathForResponsiveness(true)) // Use aggressive override timing
                        {
                            CoPilot.Instance.LogMessage("IMMEDIATE OVERRIDE: 180 detected after dash positioning - overriding with new position!");
                            ClearPathForEfficiency();
                            
                            // INSTANT OVERRIDE: Position cursor to correct location and dash there instead
                            if (followTarget?.Pos != null)
                            {
                                var correctScreenPos = Helper.WorldToValidScreenPosition(followTarget.Pos);
                                yield return Mouse.SetCursorPosHuman(correctScreenPos);
                                Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
                                lastDashTime = DateTime.Now; // Record dash time for cooldown
                                CoPilot.Instance.LogMessage("DASH OVERRIDE: Dashed to correct position to override old dash");
                            }
                            yield return null;
                            continue;
                        }
                        
                        if (instantPathOptimization)
                        {
                            // INSTANT MODE: Skip delays for immediate path correction
                            CoPilot.Instance.LogMessage("INSTANT PATH OPTIMIZATION: Dash task with no delays");
                            Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
                            lastDashTime = DateTime.Now; // Record dash time for cooldown
                            instantPathOptimization = false; // Reset flag after use
                        }
                        else
                        {
                            // Normal delays
                            yield return new WaitTime(random.Next(25) + 30);
                            Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
                            lastDashTime = DateTime.Now; // Record dash time for cooldown
                            yield return new WaitTime(random.Next(25) + 30);
                        }
                        yield return null;
                        continue;
                    }

                    if (shouldTeleportConfirmAndContinue)
                    {
                        yield return Mouse.SetCursorPosHuman(new Vector2(currentTask.WorldPosition.X, currentTask.WorldPosition.Y));
                        yield return new WaitTime(200);
                        yield return Mouse.LeftClick();
                        yield return new WaitTime(1000);
                        yield return null;
                        continue;
                    }

                    if (shouldTeleportButtonAndContinue)
                    {
                        yield return Mouse.SetCursorPosHuman(new Vector2(currentTask.WorldPosition.X, currentTask.WorldPosition.Y), false);
                        yield return new WaitTime(200);
                        yield return Mouse.LeftClick();
                        yield return new WaitTime(200);
                        yield return null;
                        continue;
                    }
                }
            }

            lastPlayerPosition = CoPilot.Instance.playerPosition;
            yield return new WaitTime(50);
        }
        // ReSharper disable once IteratorNeverReturns
    }

    // New method for decision making that runs every game tick
    public void UpdateAutoPilotLogic()
    {
        try
        {
            if (!CoPilot.Instance.Settings.Enable.Value || !CoPilot.Instance.Settings.autoPilotEnabled.Value || CoPilot.Instance.localPlayer == null || !CoPilot.Instance.localPlayer.IsAlive || 
                !CoPilot.Instance.GameController.IsForeGroundCache || MenuWindow.IsOpened || CoPilot.Instance.GameController.IsLoading || !CoPilot.Instance.GameController.InGame)
            {
                return;
            }

            // Update player position for responsiveness detection - MORE FREQUENT UPDATES
            lastPlayerPosition = CoPilot.Instance.playerPosition;

            //Cache the current follow target (if present)
            followTarget = GetFollowingTarget();
            var leaderPartyElement = GetLeaderPartyElement();

            if (followTarget == null && leaderPartyElement != null && !leaderPartyElement.ZoneName.Equals(CoPilot.Instance.GameController?.Area.CurrentArea.DisplayName)) 
            {
                var portal = GetBestPortalLabel(leaderPartyElement);
                if (portal != null) 
                {
                    // Hideout -> Map || Chamber of Sins A7 -> Map
                    tasks.Add(new TaskNode(portal, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value, TaskNodeType.Transition));
                } 
                else 
                {
                    // Swirly-able (inverted due to overlay)
                    var tpConfirmation = GetTpConfirmation();
                    if (tpConfirmation != null)
                    {
                        // Add teleport confirmation task
                        var center = tpConfirmation.GetClientRect().Center;
                        tasks.Add(new TaskNode(new Vector3(center.X, center.Y, 0), 0, TaskNodeType.TeleportConfirm));
                    }
                    else
                    {
                        // Add teleport button task
                        var tpButton = leaderPartyElement != null ? GetTpButton(leaderPartyElement) : Vector2.Zero;
                        if(!tpButton.Equals(Vector2.Zero))
                        {
                            tasks.Add(new TaskNode(new Vector3(tpButton.X, tpButton.Y, 0), 0, TaskNodeType.TeleportButton));
                        }
                    }
                }
            }
            else if (followTarget == null) 
            {
                // Leader is not in current zone - look for portals to follow them
                if (leaderPartyElement != null && !leaderPartyElement.ZoneName.Equals(CoPilot.Instance.GameController?.Area.CurrentArea.DisplayName))
                {
                    // Leader is in different zone, look for portals
                    var portal = GetBestPortalLabel(leaderPartyElement);
                    if (portal != null)
                    {
                        // Clear any existing movement tasks and add portal task
                        tasks.RemoveAll(t => t.Type == TaskNodeType.Movement);
                        tasks.Add(new TaskNode(portal, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value, TaskNodeType.Transition));
                    }
                }
                else
                {
                    // Leader party element not available or in same zone, clear movement tasks
                    tasks.RemoveAll(t => t.Type == TaskNodeType.Movement);
                }
            } 
            else if (followTarget != null)
            {
                // CHECK RESPONSIVENESS FIRST - Clear paths when player moves significantly
                if (ShouldClearPathForResponsiveness())
                {
                    CoPilot.Instance.LogMessage("RESPONSIVENESS: Preventing inefficient path creation - clearing for better tracking");
                    instantPathOptimization = true; // Enable instant mode for immediate response
                    ClearPathForEfficiency();
                    
                    // FORCE IMMEDIATE PATH RECALCULATION - Skip normal logic and create direct path
                    if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                    {
                        var instantDistanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                        CoPilot.Instance.LogMessage($"RESPONSIVENESS: Creating direct path to leader - Distance: {instantDistanceToLeader:F1}");
                        
                        if (instantDistanceToLeader > 1000 && CoPilot.Instance.Settings.autoPilotDashEnabled) // Increased from 700 to 1000
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, 0, TaskNodeType.Dash));
                        }
                        else
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                        }
                    }
                    return; // Skip the rest of the path creation logic
                }

                // CHECK PATH EFFICIENCY BEFORE CREATING NEW PATHS - PREVENT INEFFICIENT PATHS
                if (ShouldAbandonPathForEfficiency())
                {
                    CoPilot.Instance.LogMessage("INSTANT PATH OPTIMIZATION: Preventing inefficient path creation");
                    instantPathOptimization = true; // Enable instant mode for immediate response
                    ClearPathForEfficiency();
                    
                    // FORCE IMMEDIATE PATH RECALCULATION - Skip normal logic and create direct path
                    if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                    {
                        var instantDistanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                        CoPilot.Instance.LogMessage($"INSTANT PATH OPTIMIZATION: Creating direct path to leader - Distance: {instantDistanceToLeader:F1}");
                        
                        if (instantDistanceToLeader > 1000 && CoPilot.Instance.Settings.autoPilotDashEnabled) // Increased from 700 to 1000
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, 0, TaskNodeType.Dash));
                        }
                        else
                        {
                            tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                        }
                    }
                    return; // Skip the rest of the path creation logic
                }

                // TODO: If in town, do not follow (optional)
                var distanceToLeader = Vector3.Distance(CoPilot.Instance.playerPosition, followTarget.Pos);
                //We are NOT within clear path distance range of leader. Logic can continue
                if (distanceToLeader >= CoPilot.Instance.Settings.autoPilotClearPathDistance.Value)
                {
                    //Leader moved VERY far in one frame. Check for transition to use to follow them.
                    var distanceMoved = Vector3.Distance(lastTargetPosition, followTarget.Pos);
                    if (lastTargetPosition != Vector3.Zero && distanceMoved > CoPilot.Instance.Settings.autoPilotClearPathDistance.Value)
                    {
                        var transition = GetBestPortalLabel(leaderPartyElement);
                        // Check for Portal within Screen Distance.
                        if (transition != null && transition.ItemOnGround.DistancePlayer < 80)
                            tasks.Add(new TaskNode(transition,200, TaskNodeType.Transition));
                    }
                    //We have no path, set us to go to leader pos.
                    else if (tasks.Count == 0 && distanceMoved < 2000 && distanceToLeader > 200 && distanceToLeader < 2000)
                    {
                        // Validate followTarget position before creating tasks
                        if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                        {
                            // If very far away, add dash task instead of movement task
                            if (distanceToLeader > 1000 && CoPilot.Instance.Settings.autoPilotDashEnabled) // Increased from 700 to 1000
                            {
                                CoPilot.Instance.LogMessage($"Adding Dash task - Distance: {distanceToLeader:F1}, Dash enabled: {CoPilot.Instance.Settings.autoPilotDashEnabled}");
                                tasks.Add(new TaskNode(followTarget.Pos, 0, TaskNodeType.Dash));
                            }
                            else
                            {
                                CoPilot.Instance.LogMessage($"Adding Movement task - Distance: {distanceToLeader:F1}, Dash enabled: {CoPilot.Instance.Settings.autoPilotDashEnabled}, Dash threshold: 700");
                                tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                            }
                        }
                        else
                        {
                            CoPilot.Instance.LogError($"Invalid followTarget position: {followTarget?.Pos}, skipping task creation");
                        }
                    }
                    //We have a path. Check if the last task is far enough away from current one to add a new task node.
                    else if (tasks.Count > 0)
                    {
                        if (followTarget?.Pos != null && !float.IsNaN(followTarget.Pos.X) && !float.IsNaN(followTarget.Pos.Y) && !float.IsNaN(followTarget.Pos.Z))
                        {
                            var distanceFromLastTask = Vector3.Distance(tasks.Last().WorldPosition, followTarget.Pos);
                            // More responsive: reduce threshold by half for more frequent path updates
                            var responsiveThreshold = CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value / 2;
                            if (distanceFromLastTask >= responsiveThreshold)
                            {
                                CoPilot.Instance.LogMessage($"RESPONSIVENESS: Adding new path node - Distance: {distanceFromLastTask:F1}, Threshold: {responsiveThreshold:F1}");
                                tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                            }
                        }
                    }
                }
                else
                {
                    //Clear all tasks except for looting/claim portal (as those only get done when we're within range of leader. 
                    if (tasks.Count > 0)
                    {
                        for (var i = tasks.Count - 1; i >= 0; i--)
                            if (tasks[i].Type == TaskNodeType.Movement || tasks[i].Type == TaskNodeType.Transition)
                                tasks.RemoveAt(i);
                    }
                    if (CoPilot.Instance.Settings.autoPilotCloseFollow.Value)
                    {
                        //Close follow logic. We have no current tasks. Check if we should move towards leader
                        if (distanceToLeader >= CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance.Value)
                            tasks.Add(new TaskNode(followTarget.Pos, CoPilot.Instance.Settings.autoPilotPathfindingNodeDistance));
                    }

                    //Check if we should add quest loot logic. We're close to leader already
                    var questLoot = GetQuestItem();
                    if (questLoot != null &&
                        Vector3.Distance(CoPilot.Instance.playerPosition, questLoot.Pos) < CoPilot.Instance.Settings.autoPilotClearPathDistance.Value &&
                        tasks.FirstOrDefault(I => I.Type == TaskNodeType.Loot) == null)
                        tasks.Add(new TaskNode(questLoot.Pos, CoPilot.Instance.Settings.autoPilotClearPathDistance, TaskNodeType.Loot));

                    else if (!hasUsedWp && CoPilot.Instance.Settings.autoPilotTakeWaypoints)
                    {
                        //Check if there's a waypoint nearby
                        var waypoint = CoPilot.Instance.GameController.EntityListWrapper.Entities.SingleOrDefault(I => I.Type ==EntityType.Waypoint &&
                                                                                                                       Vector3.Distance(CoPilot.Instance.playerPosition, I.Pos) < CoPilot.Instance.Settings.autoPilotClearPathDistance);

                        if (waypoint != null)
                        {
                            hasUsedWp = true;
                            tasks.Add(new TaskNode(waypoint.Pos, CoPilot.Instance.Settings.autoPilotClearPathDistance, TaskNodeType.ClaimWaypoint));
                        }
                    }
                }
            }
            if (followTarget?.Pos != null)
                lastTargetPosition = followTarget.Pos;
        }
        catch (Exception e)
        {
            CoPilot.Instance.LogError($"UpdateAutoPilotLogic Error: {e}");
        }
    }
    // ReSharper disable once IteratorNeverReturns

    private bool CheckDashTerrain(Vector2 targetPosition)
    {
        if (tiles == null)
            return false;
        //TODO: Completely re-write this garbage. 
        //It's not taking into account a lot of stuff, horribly inefficient and just not the right way to do this.
        //Calculate the straight path from us to the target (this would be waypoints normally)
        var dir = targetPosition - CoPilot.Instance.GameController.Player.GridPos;
        dir.Normalize();

        var distanceBeforeWall = 0;
        var distanceInWall = 0;

        var shouldDash = false;
        var points = new List<System.Drawing.Point>();
        for (var i = 0; i < 500; i++)
        {
            var v2Point = CoPilot.Instance.GameController.Player.GridPos + i * dir;
            var point = new System.Drawing.Point((int)(CoPilot.Instance.GameController.Player.GridPos.X + i * dir.X),
                (int)(CoPilot.Instance.GameController.Player.GridPos.Y + i * dir.Y));

            if (points.Contains(point))
                continue;
            if (Vector2.Distance(v2Point,targetPosition) < 2)
                break;

            points.Add(point);
            var tile = tiles[point.X, point.Y];


            //Invalid tile: Block dash
            if (tile == 255)
            {
                shouldDash = false;
                break;
            }
            else if (tile == 2)
            {
                if (shouldDash)
                    distanceInWall++;
                shouldDash = true;
            }
            else if (!shouldDash)
            {
                distanceBeforeWall++;
                if (distanceBeforeWall > 10)					
                    break;					
            }
        }

        if (distanceBeforeWall > 10 || distanceInWall < 5)
            shouldDash = false;

        if (shouldDash)
        {
            Mouse.SetCursorPos(Helper.WorldToValidScreenPosition(targetPosition.GridToWorld(followTarget == null ? CoPilot.Instance.GameController.Player.Pos.Z : followTarget.Pos.Z)));
            Keyboard.KeyPress(CoPilot.Instance.Settings.autoPilotDashKey);
            return true;
        }

        return false;
    }

    private Entity GetFollowingTarget()
    {
        try
        {
            string leaderName = CoPilot.Instance.Settings.autoPilotLeader.Value.ToLower();
            return CoPilot.Instance.GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Player].FirstOrDefault(x => string.Equals(x.GetComponent<Player>()?.PlayerName.ToLower(), leaderName, StringComparison.OrdinalIgnoreCase));
        }
        // Sometimes we can get "Collection was modified; enumeration operation may not execute" exception
        catch
        {
            return null;
        }
    }

    private static Entity GetQuestItem()
    {
        try
        {
            return CoPilot.Instance.GameController.EntityListWrapper.Entities
                .Where(e => e?.Type == EntityType.WorldItem && e.IsTargetable && e.HasComponent<WorldItem>())
                .FirstOrDefault(e =>
                {
                    var itemEntity = e.GetComponent<WorldItem>().ItemEntity;
                    return CoPilot.Instance.GameController.Files.BaseItemTypes.Translate(itemEntity.Path).ClassName ==
                           "QuestItem";
                });
        }
        catch
        {
            return null;
        }
    }

    public void Render()
    {
        if (CoPilot.Instance.Settings.autoPilotToggleKey.PressedOnce())
        {
            CoPilot.Instance.Settings.autoPilotEnabled.SetValueNoEvent(!CoPilot.Instance.Settings.autoPilotEnabled.Value);
            tasks = new List<TaskNode>();				
        }

        // Restart coroutine if it died
        if (CoPilot.Instance.Settings.autoPilotEnabled && (autoPilotCoroutine == null || !autoPilotCoroutine.Running))
        {
            CoPilot.Instance.LogMessage("AutoPilot: Restarting coroutine - it was dead");
            StartCoroutine();
        }
        else if (CoPilot.Instance.Settings.autoPilotEnabled)
        {
            if (tasks?.Count > 0)
            {
                CoPilot.Instance.LogMessage($"AutoPilot: Coroutine status - Running: {autoPilotCoroutine?.Running}, Task count: {tasks?.Count ?? 0}, First task: {tasks[0].Type}");
            }
            else
            {
                CoPilot.Instance.LogMessage($"AutoPilot: Coroutine status - Running: {autoPilotCoroutine?.Running}, Task count: {tasks?.Count ?? 0}");
            }
        }

        if (!CoPilot.Instance.Settings.autoPilotEnabled || CoPilot.Instance.GameController.IsLoading || !CoPilot.Instance.GameController.InGame)
            return;

        try
        {
            var portalLabels =
                CoPilot.Instance.GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels.Where(x =>
                    x != null && x.IsVisible && x.Label != null && x.Label.IsValid && x.Label.IsVisible &&
                    x.ItemOnGround != null &&
                    (x.ItemOnGround.Metadata.ToLower().Contains("areatransition") ||
                     x.ItemOnGround.Metadata.ToLower().Contains("portal"))).ToList();

            foreach (var portal in portalLabels)
            {
                CoPilot.Instance.Graphics.DrawLine(portal.Label.GetClientRectCache.TopLeft, portal.Label.GetClientRectCache.TopRight, 2f,Color.Firebrick);
            }
        }
        catch (Exception)
        {
            //ignore
        }
        /*
        // Debug for UI Element
        try
        {
            foreach (var partyElementWindow in PartyElements.GetPlayerInfoElementList())
            {
                if (string.Equals(partyElementWindow.PlayerName.ToLower(), CoPilot.instance.Settings.autoPilotLeader.Value.ToLower(), StringComparison.CurrentCultureIgnoreCase))
                {
                    var windowOffset = CoPilot.instance.GameController.Window.GetWindowRectangle().TopLeft;

                    var elemCenter = partyElementWindow.TPButton.GetClientRectCache.Center;
                    var finalPos = new Vector2(elemCenter.X + windowOffset.X, elemCenter.Y + windowOffset.Y);

                    CoPilot.instance.Graphics.DrawText("Offset: " +windowOffset.ToString("F2"),new Vector2(300, 560));
                    CoPilot.instance.Graphics.DrawText("Element: " +elemCenter.ToString("F2"),new Vector2(300, 580));
                    CoPilot.instance.Graphics.DrawText("Final: " +finalPos.ToString("F2"),new Vector2(300, 600));
                }
            }
        }
        catch (Exception e)
        {

        }
        */

        // Cache Task to prevent access while Collection is changing.
        try
        {
            var taskCount = 0;
            var dist = 0f;
            var cachedTasks = tasks;
            if (cachedTasks?.Count > 0)
            {
                CoPilot.Instance.Graphics.DrawText(
                    "Current Task: " + cachedTasks[0].Type,
                    new Vector2(500, 160));
                foreach (var task in cachedTasks.TakeWhile(task => task?.WorldPosition != null))
                {
                    if (taskCount == 0)
                    {
                        CoPilot.Instance.Graphics.DrawLine(
                            Helper.WorldToValidScreenPosition(CoPilot.Instance.playerPosition),
                            Helper.WorldToValidScreenPosition(task.WorldPosition), 2f, Color.Pink);
                        dist = Vector3.Distance(CoPilot.Instance.playerPosition, task.WorldPosition);
                    }
                    else
                    {
                        CoPilot.Instance.Graphics.DrawLine(Helper.WorldToValidScreenPosition(task.WorldPosition),
                            Helper.WorldToValidScreenPosition(cachedTasks[taskCount - 1].WorldPosition), 2f, Color.Pink);
                    }

                    taskCount++;
                }
            }
            if (CoPilot.Instance.localPlayer != null)
            {
                var targetDist = Vector3.Distance(CoPilot.Instance.playerPosition, lastTargetPosition);
                CoPilot.Instance.Graphics.DrawText(
                    $"Follow Enabled: {CoPilot.Instance.Settings.autoPilotEnabled.Value}", new System.Numerics.Vector2(500, 120));
                CoPilot.Instance.Graphics.DrawText(
                    $"Task Count: {taskCount:D} Next WP Distance: {dist:F} Target Distance: {targetDist:F}",
                    new System.Numerics.Vector2(500, 140));

            }
        }
        catch (Exception)
        {
            // ignored
        }

        CoPilot.Instance.Graphics.DrawText("AutoPilot: Active", new System.Numerics.Vector2(350, 120));
        CoPilot.Instance.Graphics.DrawText("Coroutine: " + (autoPilotCoroutine.Running ? "Active" : "Dead"), new System.Numerics.Vector2(350, 140));
        CoPilot.Instance.Graphics.DrawText("Leader: " + (followTarget != null ? "Found" : "Null"), new System.Numerics.Vector2(350, 160));
        CoPilot.Instance.Graphics.DrawLine(new System.Numerics.Vector2(490, 120), new System.Numerics.Vector2(490,180), 1, Color.White);
    }
}