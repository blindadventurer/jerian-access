using System.IO;
using BepInEx;
using BepInEx.Configuration;
using JerianAccess.Host;
using JerianAccess.Speech;
using UnityEngine;

namespace JerianAccess
{
    /// <summary>
    /// The permanent BepInEx entry point. Owns what can never reload: the speech pipeline (native
    /// handles), the module loader, the per-frame pump, and (Debug) the dev server. All feature
    /// code lives in JerianAccess.Module and hot-reloads through <see cref="ReloadModule"/>.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.rashadnaqeeb.jerianaccess";
        public const string Name = "Jerian Access";
        // The port's own version, not the Brante mod's it grew out of: 0.x while the game is
        // still being played through by testers.
        public const string Version = "0.9.2";

        // Public (not internal) so /eval-compiled code, which lives in its own assembly, can reach
        // the speech pipeline: `Plugin.Instance.Speech.Speak("...")` is the standard smoke test.
        public static Plugin Instance { get; private set; }

        public SpeechPipeline Speech { get; private set; }
        internal ModuleLoader Loader { get; private set; }

        private ConfigEntry<bool> _enabled;

        public bool Enabled => _enabled.Value;

        // Jerian destroys BepInEx's manager GameObject (and this component with it) seconds into
        // boot while the game keeps running - observed live 2026-07-23, no log error, mid-run.
        // Real teardown is therefore keyed off Application.quitting, never OnDestroy alone; the
        // pump lives on our own HideAndDontSave GameObject, which the purge misses.
        private static bool _quitting;

        private void Awake()
        {
            Instance = this;
            HostLog.Source = Logger;
            Application.quitting += () => _quitting = true;
            _enabled = Config.Bind("General", "Enabled", true,
                "Master enable. Off = the mod loads but does nothing (no input, no speech).");

            Speech = new SpeechPipeline(Config);

            var modDir = Path.GetDirectoryName(Info.Location);
            var host = new ModHost(this, modDir, Version);
            Loader = new ModuleLoader(Path.Combine(modDir, "module", "JerianAccess.Module.dll"), host);

            var go = new GameObject("JerianAccess.Host");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            go.AddComponent<HostPump>();

            bool loaded = Loader.Load();
            HostLog.Info(Name + " " + Version + " host up; module " + (loaded ? "generation " + Loader.Generation : "FAILED TO LOAD"));

#if DEBUG
            StartDevServer(modDir);
#endif
        }

        private void OnDestroy()
        {
            if (!_quitting)
            {
                HostLog.Warning("host component destroyed by the game mid-run; " +
                                "infrastructure stays up on the hidden pump object.");
                return;
            }
#if DEBUG
            StopDevServer();
#endif
            Loader?.Unload();
            Speech?.Shutdown();
        }

        /// <summary>Rebuild the feature module from its freshly built dll, no restart. Wired to
        /// F6 and POST /reload (Debug).</summary>
        internal void ReloadModule()
        {
            bool ok = Loader.Reload();
            // Debug-only status line, exempt from the no-inline-strings rule (dev tooling).
            Speech.Output(ok ? "module reloaded, generation " + Loader.Generation : "module reload failed, see log", interrupt: true);
        }

#if DEBUG
        private Dev.DevServer _devServer;

        private void StartDevServer(string modDir)
        {
            if (System.Environment.GetEnvironmentVariable("JERIAN_NO_DEV") == "1")
            {
                HostLog.Info("[dev] JERIAN_NO_DEV=1: dev server disabled.");
                return;
            }
            _devServer = new Dev.DevServer(this, modDir);
            _devServer.Start();
        }

        private void StopDevServer()
        {
            _devServer?.Stop();
            _devServer = null;
        }

        internal void DevTick() => _devServer?.MainThreadTick();
#endif
    }
}
