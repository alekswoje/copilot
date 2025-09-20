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

        public void Execute()
        {
            // Check if auto join party is enabled and enough time has passed since last attempt (0.5 second cooldown)
            var timeSinceLastAttempt = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
            if (_settings.autoJoinPartyEnabled && timeSinceLastAttempt >= 0.5 && _instance.Gcd())
            {
                // Debug: Always log when executing to see if this method is being called
                BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Execute called - enabled: {_settings.autoJoinPartyEnabled}, time since last: {timeSinceLastAttempt:F1}s");
                try
                {
                    // Check if player is already in a party - if so, don't accept invites
                    var partyElement = _instance.GetPartyElements();
                    var isInParty = partyElement != null && partyElement.Count > 0;

                    if (isInParty)
                    {
                        // Only log this occasionally to avoid spam (every 15 seconds)
                        if (timeSinceLastAttempt >= 15.0)
                        {
                            BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Player already in party ({partyElement.Count} members)");
                        }
                        // Still update the cooldown to prevent spam
                        _lastAutoJoinPartyAttempt = DateTime.Now;
                        return;
                    }

                    // Check if the invites panel is visible
                    var invitesPanel = _instance.GetInvitesPanel();
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
                                        Thread.Sleep(300);

                                        // Verify mouse position
                                        var currentMousePos = _instance.GetMousePosition();
                                        var distanceFromTarget = Vector2.Distance(currentMousePos, buttonCenter);
                                        BetterFollowbotLite.Instance.LogMessage($"AUTO JOIN PARTY: Mouse distance from target: {distanceFromTarget:F1}");

                                        if (distanceFromTarget < 15) // Allow slightly more tolerance
                                        {
                                            // Perform click with verification
                                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Performing left click on accept button");

                                            // First click attempt - use synchronous mouse events
                                            Mouse.LeftMouseDown();
                                            Thread.Sleep(40);
                                            Mouse.LeftMouseUp();
                                            Thread.Sleep(300); // Longer delay

                                            // Check if we successfully joined a party
                                            var partyAfterClick = _instance.GetPartyElements();
                                            var joinedParty = partyAfterClick != null && partyAfterClick.Count > 0;

                                            if (joinedParty)
                                            {
                                                BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Successfully joined party!");
                                            }
                                            else
                                            {
                                                // Second click attempt with longer delay
                                                Thread.Sleep(600);
                                                Mouse.LeftMouseDown();
                                                Thread.Sleep(40);
                                                Mouse.LeftMouseUp();
                                                Thread.Sleep(300);

                                                // Check again
                                                partyAfterClick = _instance.GetPartyElements();
                                                joinedParty = partyAfterClick != null && partyAfterClick.Count > 0;

                                                if (joinedParty)
                                                {
                                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Successfully joined party on second attempt!");
                                                }
                                                else
                                                {
                                                    // Only log failures occasionally to avoid spam
                                                    var timeSinceLastFailure = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
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
                                        _instance.lastTimeAny = DateTime.Now;
                                        _lastAutoJoinPartyAttempt = DateTime.Now;
                                    }
                                    else
                                    {
                                        // Only log button not found occasionally to avoid spam
                                        var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                                        if (timeSinceLastLog >= 20.0)
                                        {
                                            BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: Accept button not found or not visible");
                                        }
                                    }
                                }
                                else
                                {
                                    // Only log navigation failures occasionally to avoid spam
                                    var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                                    if (timeSinceLastLog >= 30.0)
                                    {
                                        BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: UI hierarchy navigation failed");
                                    }
                                }
                            }
                            else
                            {
                                var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
                                if (timeSinceLastLog >= 30.0)
                                {
                                    BetterFollowbotLite.Instance.LogMessage("AUTO JOIN PARTY: UI hierarchy navigation failed");
                                }
                            }
                        }
                        else
                        {
                            var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
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
                        var timeSinceLastLog = (DateTime.Now - _lastAutoJoinPartyAttempt).TotalSeconds;
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
        }
    }
}
