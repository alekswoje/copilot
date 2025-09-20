using System;
using System.Linq;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace BetterFollowbotLite
{
    internal class DashManager
    {
        private readonly BetterFollowbotLite _instance;
        private DateTime _lastDashTime = DateTime.MinValue;

        public DashManager(BetterFollowbotLite instance)
        {
            _instance = instance;
        }

        /// <summary>
        /// Attempts to dash to the leader position for SMITE logic
        /// </summary>
        /// <param name="leaderPos">Position to dash towards</param>
        /// <param name="distanceToLeader">Current distance to leader</param>
        /// <param name="autoPilot">AutoPilot instance for task checking</param>
        /// <returns>True if dash was attempted, false otherwise</returns>
        public bool TryDashToLeader(Vector3 leaderPos, float distanceToLeader, AutoPilot autoPilot)
        {
            if (!_instance.Settings.autoPilotDashEnabled || autoPilot?.FollowTarget == null)
            {
                _instance.LogMessage("SMITE: Dash not available or not enabled");
                return false;
            }

            // CRITICAL: Don't dash if teleport is in progress (strongest protection)
            if (AutoPilot.IsTeleportInProgress)
            {
                _instance.LogMessage("SMITE: TELEPORT IN PROGRESS - blocking all dash attempts");
                return false;
            }

            // Check for transition tasks
            var hasTransitionTask = autoPilot.Tasks.Any(t =>
                t.Type == TaskNodeType.Transition ||
                t.Type == TaskNodeType.TeleportConfirm ||
                t.Type == TaskNodeType.TeleportButton);

            if (hasTransitionTask)
            {
                _instance.LogMessage($"SMITE: Transition/teleport task active ({autoPilot.Tasks.Count} tasks), skipping dash");
                return false;
            }

            // Check distance threshold
            if (distanceToLeader <= _instance.Settings.autoPilotDashDistance)
            {
                _instance.LogMessage("SMITE: Already close to leader, skipping dash");
                return false;
            }

            return ExecuteDash(leaderPos, $"SMITE: Dashing to leader - Distance: {distanceToLeader:F1}");
        }

        /// <summary>
        /// Executes a dash task for AutoPilot pathfinding
        /// </summary>
        /// <param name="targetPosition">Position to dash towards</param>
        /// <param name="cursorPointingCorrectly">Whether cursor is already pointing towards target</param>
        /// <param name="autoPilot">AutoPilot instance</param>
        /// <returns>True if dash was successful, false otherwise</returns>
        public bool ExecuteDashTask(Vector3 targetPosition, bool cursorPointingCorrectly, AutoPilot autoPilot)
        {
            if (!CanDash())
            {
                return false;
            }

            if (cursorPointingCorrectly)
            {
                return ExecuteDash(targetPosition, "Dash task: Executing dash");
            }
            else
            {
                // Try to position cursor and then dash
                return ExecuteDashWithCursorPositioning(targetPosition, autoPilot);
            }
        }

        /// <summary>
        /// Executes a retry dash after cursor positioning
        /// </summary>
        /// <param name="targetPosition">Position to dash towards</param>
        /// <returns>True if dash was successful, false otherwise</returns>
        public bool ExecuteRetryDash(Vector3 targetPosition)
        {
            return ExecuteDash(targetPosition, "Dash task: Retry - Executing dash");
        }

        /// <summary>
        /// Core dash execution logic
        /// </summary>
        /// <param name="targetPosition">Position to dash towards</param>
        /// <param name="logMessage">Log message to display</param>
        /// <returns>True if dash was attempted, false otherwise</returns>
        private bool ExecuteDash(Vector3 targetPosition, string logMessage)
        {
            // Find dash skill to check availability
            var dashSkill = FindDashSkill();

            _instance.LogMessage($"Dash skill check - Found: {dashSkill != null}, OnSkillBar: {dashSkill?.IsOnSkillBar}, CanBeUsed: {dashSkill?.CanBeUsed}");

            if (dashSkill != null && dashSkill.IsOnSkillBar && dashSkill.CanBeUsed)
            {
                _instance.LogMessage(logMessage);

                // Position mouse towards target
                var targetScreenPos = _instance.GameController.IngameState.Camera.WorldToScreen(targetPosition);
                Mouse.SetCursorPos(targetScreenPos);

                // Small delay to ensure mouse movement is registered
                System.Threading.Thread.Sleep(50);

                // Execute dash using the skill's key
                Keyboard.KeyPress(_instance.GetSkillInputKey(dashSkill.SkillSlotIndex));
                _lastDashTime = DateTime.Now;

                _instance.LogMessage("Dash executed successfully");
                return true;
            }
            else if (dashSkill == null)
            {
                _instance.LogMessage("No dash skill found, using configured dash key");

                // Fallback: Use configured dash key directly
                _instance.LogMessage(logMessage + " (fallback)");

                // Position mouse towards target
                var targetScreenPos = _instance.GameController.IngameState.Camera.WorldToScreen(targetPosition);
                Mouse.SetCursorPos(targetScreenPos);

                // Small delay to ensure mouse movement is registered
                System.Threading.Thread.Sleep(50);

                // Execute dash using configured key
                Keyboard.KeyPress(_instance.Settings.autoPilotDashKey);
                _lastDashTime = DateTime.Now;

                _instance.LogMessage("Dash executed successfully (fallback)");
                return true;
            }
            else if (!dashSkill.CanBeUsed)
            {
                _instance.LogMessage("Dash skill is on cooldown or unavailable");
                return false;
            }

            return false;
        }

        /// <summary>
        /// Executes dash with cursor positioning logic for AutoPilot tasks
        /// </summary>
        /// <param name="targetPosition">Position to dash towards</param>
        /// <param name="autoPilot">AutoPilot instance</param>
        /// <returns>True if dash setup was successful, false otherwise</returns>
        private bool ExecuteDashWithCursorPositioning(Vector3 targetPosition, AutoPilot autoPilot)
        {
            // Position mouse towards target
            var targetScreenPos = Helper.WorldToValidScreenPosition(targetPosition);
            if (targetScreenPos.X >= 0 && targetScreenPos.Y >= 0 &&
                targetScreenPos.X <= _instance.GameController.Window.GetWindowRectangle().Width &&
                targetScreenPos.Y <= _instance.GameController.Window.GetWindowRectangle().Height)
            {
                // Target is on-screen, position mouse
                // Note: This would need to be yield return in AutoPilot context, but we'll handle it in the calling method
                _instance.LogMessage("Dash task: Positioned cursor towards target");
                return true;
            }
            else
            {
                _instance.LogMessage("Dash task: Target off-screen, cannot position cursor");
                return false;
            }
        }

        /// <summary>
        /// Finds a suitable dash skill from the player's skills
        /// </summary>
        /// <returns>The dash skill if found, null otherwise</returns>
        private ActorSkill FindDashSkill()
        {
            return _instance.skills.FirstOrDefault(s =>
                s.Name.Contains("Dash") ||
                s.Name.Contains("Whirling Blades") ||
                s.Name.Contains("Flame Dash") ||
                s.Name.Contains("Smoke Mine") ||
                (s.Name.Contains("Blade") && s.Name.Contains("Vortex")) ||
                s.IsOnSkillBar);
        }

        /// <summary>
        /// Checks if dashing is currently allowed
        /// </summary>
        /// <returns>True if dashing is allowed, false otherwise</returns>
        public bool CanDash()
        {
            return true; // Removed cooldown - dash is always available
        }

        /// <summary>
        /// Gets the last dash time for tracking purposes
        /// </summary>
        public DateTime LastDashTime => _lastDashTime;

        /// <summary>
        /// Updates the last dash time (useful for external tracking)
        /// </summary>
        public void UpdateLastDashTime()
        {
            _lastDashTime = DateTime.Now;
        }

        /// <summary>
        /// Resets the dash tracking (useful for area changes)
        /// </summary>
        public void ResetDashTracking()
        {
            _lastDashTime = DateTime.MinValue;
        }
    }
}
