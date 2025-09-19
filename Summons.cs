using System.Linq;
using ExileCore.PoEMemory.Components;

namespace BetterFollowbotLite;

internal class Summons
{
    public static float GetLowestMinionHpp()
    {
        float hpp = 100;
        foreach (var obj in BetterFollowbotLite.Instance.localPlayer.GetComponent<Actor>().DeployedObjects
                     .Where(x => x?.Entity?.GetComponent<Life>() != null))
            if (obj.Entity.GetComponent<Life>().HPPercentage < hpp)
                hpp = obj.Entity.GetComponent<Life>().HPPercentage;
        return hpp;
    }

    public static float GetAnimatedGuardianHpp()
    {
        const float hpp = 100;
        DeployedObject animatedGuardian = null;
        animatedGuardian = BetterFollowbotLite.Instance.localPlayer.GetComponent<Actor>().DeployedObjects.FirstOrDefault(x =>
            x?.Entity?.GetComponent<Life>() != null && x.Entity.Path.Contains("AnimatedArmour"));
        return animatedGuardian?.Entity.GetComponent<Life>().HPPercentage ?? hpp;
    }

    public static int GetSkeletonCount()
    {
        try
        {
            return BetterFollowbotLite.Instance.localPlayer.GetComponent<Actor>().DeployedObjects
                .Count(x => x?.Entity != null && x.Entity.IsAlive &&
                           (x.Entity.Path.Contains("Skeleton") ||
                            x.Entity.Path.Contains("skeleton") ||
                            x.Entity.Metadata.ToLower().Contains("skeleton")));
        }
        catch
        {
            return 0;
        }
    }

    public static int GetRagingSpiritCount()
    {
        try
        {
            // Try to get the count from the SummonRagingSpirit skill's DeployedEntities first (most accurate)
            var srsSkill = BetterFollowbotLite.Instance.skills?.FirstOrDefault(s =>
                s?.Name != null && (s.Name.Contains("SummonRagingSpirit") ||
                                    s.Name.Contains("Summon Raging Spirit") ||
                                    (s.Name.Contains("summon") && s.Name.Contains("spirit") && s.Name.Contains("rag"))));

            if (srsSkill != null && srsSkill.DeployedEntities != null)
            {
                // Use the skill's DeployedEntities array for most accurate count
                return srsSkill.DeployedEntities.Count(x => x?.Entity != null && x.Entity.IsAlive);
            }

            // Fallback: Count raging spirits from deployed objects
            var deployedSpirits = BetterFollowbotLite.Instance.localPlayer.GetComponent<Actor>().DeployedObjects
                .Count(x => x?.Entity != null && x.Entity.IsAlive &&
                           (x.Entity.Path.Contains("RagingSpirit") ||
                            x.Entity.Path.Contains("ragingspirit") ||
                            x.Entity.Metadata.ToLower().Contains("ragingspirit") ||
                            x.Entity.Metadata.ToLower().Contains("spirit") && x.Entity.Metadata.ToLower().Contains("rag")));

            return deployedSpirits;
        }
        catch
        {
            return 0;
        }
    }

    public static int GetTotalMinionCount()
    {
        try
        {
            return BetterFollowbotLite.Instance.localPlayer.GetComponent<Actor>().DeployedObjects
                .Count(x => x?.Entity != null && x.Entity.IsAlive &&
                           (x.Entity.Path.Contains("Skeleton") ||
                            x.Entity.Path.Contains("skeleton") ||
                            x.Entity.Metadata.ToLower().Contains("skeleton") ||
                            x.Entity.Path.Contains("RagingSpirit") ||
                            x.Entity.Path.Contains("ragingspirit") ||
                            x.Entity.Metadata.ToLower().Contains("ragingspirit") ||
                            (x.Entity.Metadata.ToLower().Contains("spirit") && x.Entity.Metadata.ToLower().Contains("rag"))));
        }
        catch
        {
            return 0;
        }
    }
}