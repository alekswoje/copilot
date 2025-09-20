using System;
using System.Threading;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;

namespace BetterFollowbotLite.Automation
{
    internal class PartyJoiner
    {
        private readonly BetterFollowbotLite _instance;
        private readonly BetterFollowbotLiteSettings _settings;
        private DateTime _lastAutoJoinPartyAttempt;

        public PartyJoiner(BetterFollowbotLite instance, BetterFollowbotLiteSettings settings)
        {
            _instance = instance;
            _settings = settings;
            _lastAutoJoinPartyAttempt = DateTime.MinValue;
        }

        // Method to get trade panel (used internally by this class)
        private dynamic GetTradePanel()
        {
            try
            {
                return BetterFollowbotLite.Instance.GameController.IngameState.IngameUi.TradeWindow;
            }
            catch
            {
                return null;
            }
        }

        public void Execute()
        {
            // Check if auto join party & accept trade is enabled and enough time has passed since last attempt (0.5 second cooldown)
            var timeSinceLastAttempt = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
            if (_settings.autoJoinPartyEnabled && timeSinceLastAttempt >= 0.5 && _instance.Gcd())
            {
                // Debug: Always log when executing to see if this method is being called
                BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Execute called - enabled: {_settings.autoJoinPartyEnabled}, time since last: {timeSinceLastAttempt:F1}s");
                try
                {
                    // Check if player is already in a party - if so, don't accept party invites (but still accept trades)
                    var partyElement = _instance.GetPartyElements();
                    var isInParty = partyElement != null && partyElement.Count > 0;

                    // Check if the invites panel is visible
                    var invitesPanel = _instance.GetInvitesPanel();
                    if (invitesPanel != null && invitesPanel.IsVisible)
                    {
                        // Get the invites array
                        var invites = invitesPanel.Invites;
                        if (invites != null && invites.Length > 0)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Found {invites.Length} invite(s)");

                            // Process each invite in the array
                            foreach (var invite in invites)
                            {
                                if (invite != null)
                                {
                                    try
                                    {
                                        // Check the action text to determine invite type
                                        var actionText = invite.ActionText;
                                        if (actionText != null)
                                        {
                                            string inviteType = "";
                                            bool shouldProcess = false;

                                            if (actionText.Contains("party invite") || actionText.Contains("sent you a party invite"))
                                            {
                                                inviteType = "PARTY";
                                                // Only process party invites if not already in party
                                                shouldProcess = !isInParty;
                                                if (isInParty)
                                                {
                                                    if (timeSinceLastAttempt >= 15.0)
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Skipping party invite - already in party ({partyElement.Count} members)");
                                                    }
                                                    continue;
                                                }
                                            }
                                            else if (actionText.Contains("trade request") || actionText.Contains("sent you a trade request"))
                                            {
                                                inviteType = "TRADE";
                                                // Always process trade requests
                                                shouldProcess = true;
                                            }
                                            else
                                            {
                                                BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Unknown invite type with action text: '{actionText}'");
                                                continue;
                                            }

                                            if (shouldProcess)
                                            {
                                                BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Processing {inviteType} invite");

                                                // Get the accept button
                                                var acceptButton = invite.AcceptButton;
                                                if (acceptButton != null && acceptButton.IsVisible)
                                                {
                                                    // Get the center position of the accept button
                                                    var buttonRect = acceptButton.GetClientRectCache;
                                                    var buttonCenter = buttonRect.Center;

                                                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: {inviteType} accept button position - X: {buttonCenter.X:F1}, Y: {buttonCenter.Y:F1}");

                                                    // Move mouse to the accept button
                                                    Mouse.SetCursorPos(buttonCenter);

                                                    // Wait for mouse to settle - longer delay to avoid AutoPilot interference
                                                    Thread.Sleep(300);

                                                    // Verify mouse position
                                                    var currentMousePos = _instance.GetMousePosition();
                                                    var distanceFromTarget = Vector2.Distance(currentMousePos, buttonCenter);
                                                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: {inviteType} mouse distance from target: {distanceFromTarget:F1}");

                                                    if (distanceFromTarget < 15) // Allow slightly more tolerance
                                                    {
                                                        // Perform click with verification
                                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Performing left click on {inviteType} accept button");

                                                        // First click attempt - use synchronous mouse events
                                                        Mouse.LeftMouseDown();
                                                        Thread.Sleep(40);
                                                        Mouse.LeftMouseUp();
                                                        Thread.Sleep(300); // Longer delay

                                                        // Check success based on invite type
                                                        bool success = false;
                                                        if (inviteType == "PARTY")
                                                        {
                                                            // Check if we successfully joined a party
                                                            var partyAfterClick = _instance.GetPartyElements();
                                                            success = partyAfterClick != null && partyAfterClick.Count > 0;

                                                            if (success)
                                                            {
                                                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: Successfully joined party!");
                                                            }
                                                        }
                                                        else if (inviteType == "TRADE")
                                                        {
                                                            // Check if trade window opened
                                                            var tradePanel = GetTradePanel();
                                                            success = tradePanel != null && tradePanel.IsVisible;

                                                            if (success)
                                                            {
                                                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: Successfully opened trade window!");
                                                            }
                                                        }

                                                        if (!success)
                                                        {
                                                            // Second click attempt with longer delay
                                                            Thread.Sleep(600);
                                                            Mouse.LeftMouseDown();
                                                            Thread.Sleep(40);
                                                            Mouse.LeftMouseUp();
                                                            Thread.Sleep(300);

                                                            // Check again
                                                            if (inviteType == "PARTY")
                                                            {
                                                                var partyAfterClick = _instance.GetPartyElements();
                                                                success = partyAfterClick != null && partyAfterClick.Count > 0;

                                                                if (success)
                                                                {
                                                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: Successfully joined party on second attempt!");
                                                                }
                                                            }
                                                            else if (inviteType == "TRADE")
                                                            {
                                                                var tradePanel = GetTradePanel();
                                                                success = tradePanel != null && tradePanel.IsVisible;

                                                                if (success)
                                                                {
                                                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: Successfully opened trade window on second attempt!");
                                                                }
                                                            }

                                                            if (!success)
                                                            {
                                                                // Only log failures occasionally to avoid spam
                                                                var timeSinceLastFailure = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                                                                if (timeSinceLastFailure >= 30.0)
                                                                {
                                                                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Failed to accept {inviteType} invite - may need manual intervention");
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: {inviteType} mouse positioning failed - too far from target ({distanceFromTarget:F1})");
                                                    }

                                                    // Update cooldowns
                                                    _instance.lastTimeAny = DateTime.Now;
                                                    _lastAutoJoinPartyAttempt = DateTime.Now;
                                                }
                                                else
                                                {
                                                    // Only log button not found occasionally to avoid spam
                                                    var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                                                    if (timeSinceLastLog >= 20.0)
                                                    {
                                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: {inviteType} accept button not found or not visible");
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: Invite has no action text");
                                        }
                                    }
                                    catch (Exception inviteEx)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Exception processing invite - {inviteEx.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                            if (timeSinceLastLog >= 30.0)
                            {
                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: No invites found in invites panel");
                            }
                        }
                    }
                    else
                    {
                        // Don't log when no invites are present - this is normal operation
                        // Only log occasionally if there might be an issue
                        var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                        if (timeSinceLastLog >= 60.0) // Log once per minute when no invites
                        {
                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY & ACCEPT TRADE: No invites detected");
                        }
                    }
                }
                catch (Exception e)
                {
                    BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY & ACCEPT TRADE: Exception occurred - {e.Message}");
                }
            }
        }
    }
}
