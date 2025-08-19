using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PushMod;

public class ConfigurationHandler {
    private ConfigFile config = new ConfigFile(Path.Combine(Paths.ConfigPath, $"{Plugin.Id}.cfg"), true);

    private ConfigEntry<string> _configPushKey;
    private ConfigEntry<string> _configProtectionKey;
    private ConfigEntry<bool> _configcanCharge;

    public KeyCode PushKey => ParseKeyCode(_configPushKey.Value, KeyCode.F);
    public KeyCode ProtectionKey => ParseKeyCode(_configProtectionKey.Value, KeyCode.F11);
    public bool CanCharge => _configcanCharge.Value;

    public ConfigurationHandler() {
        Plugin.Log.LogInfo("PushMod ConfigurationHandler initialising");

        _configPushKey = config.Bind(
            "Push Settings",
            "PushKey",
            "F",
            "The keyboard key used to push key. Example: F, E, G, etc."
        );
        _configProtectionKey = config.Bind(
            "Push Settings",
            "ProtectionKey",
            "F11",
            "The keyboard key used to enable protection push. Example: F, E, G, etc."
        );
        _configcanCharge = config.Bind(
            "Push Settings",
            "CanCharge",
            true,
            "The setting includes charging force when pushed"
        );

        Plugin.Log.LogInfo("PushMod Configuration loaded:");
        Plugin.Log.LogInfo($"  PushKey: {PushKey}");
        Plugin.Log.LogInfo($"  ProtectionKey: {ProtectionKey}");
        Plugin.Log.LogInfo($"  CanCharge: {CanCharge}");

        Plugin.Log.LogInfo("PushMod ConfigurationHandler initialised");
    }

    private KeyCode ParseKeyCode(string key, KeyCode fallback) {
        if (string.IsNullOrEmpty(key)) return fallback;
        if (Enum.TryParse<KeyCode>(key.Trim(), true, out KeyCode parsed))
            return parsed;
        Plugin.Log.LogWarning($"Invalid key code: {key}. Using default: {fallback}");
        return fallback;
    }
}
