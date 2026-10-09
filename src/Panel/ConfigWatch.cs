using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace Hearthwoven.Panel
{
    /// <summary>
    /// Live settings (Joost changes them in Gale while the game runs): a watcher on the mod's .cfg notes a change on its own
    /// thread; the panel reloads the file on the main thread half a second after the last change (a save writes in bursts)
    /// and applies it at once: Panel.Scale rescales the panel, the keys are read from their entries on the next frame,
    /// Dev.SampleData and Dev.PanelSnapshots switch both ways (SampleMode reads the entry live; the open panel is rebuilt).
    /// </summary>
    public partial class PanelUi
    {
        static ConfigFile watchedConfig;
        static FileSystemWatcher configWatcher;
        static volatile bool configTouched;
        static float configDue;
        const float ConfigSettle = 0.5f;

        static void WatchConfig(ConfigFile config)
        {
            watchedConfig = config;
            try
            {
                var path = config.ConfigFilePath;
                configWatcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                };
                FileSystemEventHandler touched = (_, __) => configTouched = true;
                configWatcher.Changed += touched; configWatcher.Created += touched;
                configWatcher.Renamed += (_, __) => configTouched = true;
                configWatcher.EnableRaisingEvents = true;
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] settings are read at start only (no file watch): " + e.Message); }
        }

        // every frame, open or closed (main thread): reload once the file has settled, then apply
        void LiveConfig()
        {
            var touched = configTouched; if (touched) configTouched = false;
            if (!PanelModel.ConfigDue(ref configDue, touched, Time.unscaledTime, ConfigSettle) || watchedConfig == null) return;
            try
            {
                watchedConfig.Reload();
                ApplyScale();
                if (open) { shown = null; Render(true); }   // the keys in the footer, the sample or the real data, at once
                Debug.Log("[Hearthwoven] settings reloaded: scale " + PanelModel.PanelScale(Scale.Value) + ", keys " + Hotkey.Value + "/" + InfoKey.Value + "/" + ViewKey.Value +
                          (SampleMode.On ? ", sample data on" : ""));
            }
            catch (Exception e) { Debug.LogWarning("[Hearthwoven] settings reload: " + e.Message); }
        }
    }
}
