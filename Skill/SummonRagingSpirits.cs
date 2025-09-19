using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Windows.Forms;

namespace BetterFollowbotLite.Skills
{
    internal class SummonRagingSpirits
    {
        private readonly BetterFollowbotLite _instance;
        private readonly BetterFollowbotLiteSettings _settings;
        private readonly AutoPilot _autoPilot;
        private readonly Summons _summons;

        public SummonRagingSpirits(BetterFollowbotLite instance, BetterFollowbotLiteSettings settings,
                                   AutoPilot autoPilot, Summons summons)
        {
            _instance = instance;
            _settings = settings;
            _autoPilot = autoPilot;
            _summons = summons;
        }

        // Alternative method to access entities
        private IEnumerable<Entity> GetEntities()
        {
            // Temporarily return empty collection to avoid GameController access issues
            // TODO: Implement proper entity access when framework dependencies are available
            return new List<Entity>();
        }

        public void Execute()
        {
            try
            {
                if (_settings.summonRagingSpiritsEnabled.Value && _autoPilot != null && _autoPilot.FollowTarget != null)
                {
                    var distanceToLeader = Vector3.Distance(_instance.playerPosition, _autoPilot.FollowTargetPosition);

                    // Check if we're close to the leader (within AutoPilot follow distance)
                    if (distanceToLeader <= _settings.autoPilotClearPathDistance.Value)
                    {
                        // Count current summoned minions
                        var totalMinionCount = Summons.GetSkeletonCount();

                        // Only cast SRS if we have less than the minimum required count
                        if (totalMinionCount < _settings.summonRagingSpiritsMinCount.Value)
                        {
                            // Check for rare/unique enemies within 1000 units
                            bool rareOrUniqueNearby = false;
                            var entities = GetEntities();

                            foreach (var entity in entities)
                            {
                                if (entity.IsValid && entity.IsAlive)
                                {
                                    var distanceToEntity = Vector3.Distance(_instance.playerPosition, entity.Pos);

                                    // Check if entity is within range and is rare or unique
                                    if (distanceToEntity <= 500)
                                    {
                                        var rarityComponent = entity.GetComponent<ObjectMagicProperties>();
                                        if (rarityComponent != null)
                                        {
                                            var rarity = rarityComponent.Rarity;

                                            // Always check for rare/unique
                                            if (rarity == MonsterRarity.Unique || rarity == MonsterRarity.Rare)
                                            {
                                                rareOrUniqueNearby = true;
                                                break;
                                            }
                                            // Also check for magic/white if enabled
                                            else if (_settings.summonRagingSpiritsMagicNormal.Value &&
                                                    (rarity == MonsterRarity.Magic || rarity == MonsterRarity.White))
                                            {
                                                rareOrUniqueNearby = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (rareOrUniqueNearby)
                            {
                                // Find the Summon Raging Spirits skill
                                var summonRagingSpiritsSkill = _instance.skills.FirstOrDefault(s =>
                                    s.Name.Contains("SummonRagingSpirit") ||
                                    s.Name.Contains("Summon Raging Spirit") ||
                                    (s.Name.Contains("summon") && s.Name.Contains("spirit") && s.Name.Contains("rag")));

                                if (summonRagingSpiritsSkill != null && summonRagingSpiritsSkill.IsOnSkillBar && summonRagingSpiritsSkill.CanBeUsed)
                                {
                                    var enemyType = _settings.summonRagingSpiritsMagicNormal.Value ? "Rare/Unique/Magic/White" : "Rare/Unique";
                                    _instance.LogMessage($"SRS: Current minions: {totalMinionCount}, Required: {_settings.summonRagingSpiritsMinCount.Value}, Distance to leader: {distanceToLeader:F1} (max: {_settings.autoPilotClearPathDistance.Value}), {enemyType} enemy within 500 units detected");

                                    // Use the Summon Raging Spirits skill
                                    Keyboard.KeyPress(_instance.GetSkillInputKey(summonRagingSpiritsSkill.SkillSlotIndex));
                                    _instance.lastTimeAny = DateTime.Now; // Update global cooldown

                                    _instance.LogMessage("SRS: Summoned Raging Spirit successfully");
                                }
                                else if (summonRagingSpiritsSkill == null)
                                {
                                    _instance.LogMessage("SRS: SummonRagingSpirit skill not found in skill bar");
                                }
                                else if (!summonRagingSpiritsSkill.CanBeUsed)
                                {
                                    _instance.LogMessage("SRS: SummonRagingSpirit skill is on cooldown or unavailable");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _instance.LogMessage($"SRS: Exception occurred - {e.Message}");
            }
        }
    }
}
