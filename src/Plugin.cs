using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace GhostHats
{
    /// <summary>
    /// Ghost Hats. When a player dies and turns into a spectator ghost, their hat comes with
    /// them. Purely visual and purely client-side: only players running the mod see the hats,
    /// and nothing is sent over the network.
    ///
    /// No config, on purpose. There is nothing here worth turning off and nothing worth tuning.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.exoflex.ghosthats";
        public const string PluginName = "GhostHats";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Death is no excuse for bad fashion.");
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }
}
