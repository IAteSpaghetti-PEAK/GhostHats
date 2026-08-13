using System;
using HarmonyLib;

namespace GhostHats
{
    /// <summary>
    /// RPCA_InitGhost is the buffered RPC every client (including late joiners) runs when a
    /// dead player spawns their spectator ghost, and it is where vanilla applies the rest of
    /// the ghost's cosmetics — so it is also where the owner reference is ready for us.
    /// </summary>
    [HarmonyPatch(typeof(PlayerGhost), nameof(PlayerGhost.RPCA_InitGhost))]
    internal static class PlayerGhostInitPatch
    {
        private static void Postfix(PlayerGhost __instance)
        {
            try
            {
                GhostHat.Attach(__instance);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to put a hat on a ghost: {e}");
            }
        }
    }
}
