using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using System.Linq;
using UnityEngine;

namespace PushMod;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin {
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigurationHandler PConfig { get; private set; } = null!;

    private void Awake() {
        Log = Logger;
        PConfig = new ConfigurationHandler(this);
        Log.LogInfo($"Plugin {Name} is loaded!");
        Harmony.CreateAndPatchAll(typeof(PushPatch));
    }
}

public static class PushPatch {
    /// <summary>
    /// Postfix patch on Character.Awake to attach the PushManager component.
    /// Ensures every character in the game gets a PushManager when initialized.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Character), nameof(Character.Awake))]
    public static void AwakePatch(Character __instance) {
        __instance.gameObject.AddComponent<PushManager>();
        Plugin.Log.LogInfo($"Added PushManager component to character: {__instance.characterName}");
    }
}

public class PushManager : MonoBehaviour {
    // ============================== Configuration Constants ===================================================
    private const float PUSH_RANGE = 2.5f;                      // Maximum distance for push interaction
    private const float PUSH_COOLDOWN = 1f;                     // Cooldown time between successful pushes
    private const float PUSH_FORCE_BASE = 500f;                 // Base push force applied
    private const float BINGBONG_MULTIPLIER = 10f;              // Force multiplier when holding "BingBong" item
    private const float STAMINA_COST = 0.1f;                    // Stamina consumed per push

    private const float MAX_CHARGE = 1f;                        // Maximum charge duration (seconds)
    private const float CHARGE_FORCE_MULTIPLIER = 1.5f;         // Additional force multiplier based on charge level
    private const float ANIMATION_TIME = 0.25f;                 // Fixed animation playback time
    private const float MAX_STAMINA_COST_MULTIPLIER = 3f;       // Maximum stamina cost multiplier.
    // ==========================================================================================================

    // ====================================== Debug & UI ========================================================
    private bool showProtectionUI = true;                       // Toggle display of protection status UI
    private bool showChargeBar = true;                          // Toggle display of charge progress bar
    private Color chargeBarColor = Color.cyan;                  // Color of the charge bar

    private static Texture2D? blankTexture;

    private Character localCharacter = null!;
    private Character? pushedCharacter;
    private float coolDownLeft;                                 // Remaining cooldown time before next push
    private float animationCoolDown;                            // Duration of active push animation

    private bool bingBong;                                      // True if player is holding the "BingBong" item
    private bool protectionPush;                                // If enabled, blocks incoming push forces

    // Charging system
    private bool isCharging;                                    // Whether the player is currently charging a push
    private float currentCharge;                                // Current charge level (0 to MAX_CHARGE)

    // ====================== Cached components for performance optimization ====================================
    private Character cachedCharacter = null!;
    private Camera mainCamera = null!;
    // ==========================================================================================================

    private void Awake() {
        // Cache the Character component on this GameObject
        cachedCharacter = GetComponent<Character>();
        if (cachedCharacter is null) {
            Debug.LogError("[PushManager] Character component not found on GameObject!", gameObject);
            enabled = false;
            return;
        }

        // Store reference to local player's character
        if (cachedCharacter.IsLocal) {
            localCharacter = cachedCharacter;

            // Cache main camera for raycasting
            mainCamera = Camera.main;
            if (mainCamera is null) {
                Debug.LogError("[PushManager] Main camera not found!");
                enabled = false;
            }
        }
    }

    private void Update() {

        // Toggle protection mode when the assigned key is pressed
        if (Input.GetKeyDown(Plugin.PConfig.ProtectionKey)) {
            protectionPush = !protectionPush;
            Plugin.Log.LogError($"Protection mode: {protectionPush}");
        }

        // Update cooldown timers
        if (coolDownLeft > 0f) coolDownLeft -= Time.deltaTime;
        if (animationCoolDown > 0f) animationCoolDown -= Time.deltaTime;

        // Play push animation
        if (animationCoolDown > 0f && cachedCharacter is not null) {
            PlayPushAnimation(cachedCharacter);
        }

        if (coolDownLeft > 0f) return;
        if (localCharacter is null) return;
        if (!localCharacter.view.IsMine) return;
        if (!localCharacter.data.fullyConscious) return;
        if (localCharacter.data.isCarried) return;
        if (localCharacter.data.isClimbing) return;

        // Check if the current held item is "BingBong"
        Item? currentItem = localCharacter.data.currentItem;
        bingBong = currentItem is not null && currentItem.itemTags is Item.ItemTags.BingBong; // Multi-language support 🤡

        // Handle input for charge-based pushing
        HandleChargeInput();

        // Charging is active — we update the visualization
        if (isCharging) {
            currentCharge += Time.deltaTime;
            currentCharge = Mathf.Clamp(currentCharge, 0f, MAX_CHARGE);
        }
    }

    /// <summary>
    /// Handles input for charging and releasing the push action.
    /// Charging begins on key down and applies the push on key up.
    /// </summary>
    private void HandleChargeInput() {
        if (Plugin.PConfig.CanCharge) {
            if ((Input.GetKeyDown(Plugin.PConfig.PushKey) || Input.GetKeyDown(Plugin.PConfig.SelfPushKey)) && !isCharging && coolDownLeft <= 0f) {
                isCharging = true;
                currentCharge = 0f;
                Plugin.Log.LogInfo("Started charging push...");
            }
            if (Input.GetKeyUp(Plugin.PConfig.PushKey) && !Input.GetKey(Plugin.PConfig.SelfPushKey) && isCharging) {
                isCharging = false;
                TryPushTarget(false);
            }
            if (Input.GetKeyUp(Plugin.PConfig.SelfPushKey) && !Input.GetKey(Plugin.PConfig.PushKey) && isCharging) {
                isCharging = false;
                TryPushTarget(true);
            }
        }
        else {
            if (Input.GetKeyDown(Plugin.PConfig.PushKey) && coolDownLeft <= 0f) {
                TryPushTarget(false);
            }
            if (Input.GetKeyDown(Plugin.PConfig.SelfPushKey) && coolDownLeft <= 0f) {
                TryPushTarget(true);
            }
        }
    }

    /// <summary>
    /// Attempts to perform a push using a forward raycast from the main camera.
    /// If a valid character is hit, applies force via RPC.
    /// </summary>
    private void TryPushTarget(bool self) {
        if (mainCamera is null) return;


        if (self) {
            pushedCharacter = localCharacter;
        }
        else {
            // Perform raycast from camera forward within push range
            if (!Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit hitInfo, PUSH_RANGE, LayerMask.GetMask("Character")))
                return;

            // Retrieve the Character component from the hit object
            pushedCharacter = GetCharacter(hitInfo.transform.gameObject);
            if (pushedCharacter == null || pushedCharacter == localCharacter) return;
        }

        // Calculate final push force with multipliers
        float chargeMultiplier = 1f + ((currentCharge / MAX_CHARGE) * CHARGE_FORCE_MULTIPLIER);
        float bingBongMultiplier = bingBong ? BINGBONG_MULTIPLIER : 1f;
        float totalMultiplier = bingBongMultiplier * chargeMultiplier;
        Vector3 forceDirection = mainCamera.transform.forward * PUSH_FORCE_BASE * totalMultiplier;

        Plugin.Log.LogInfo($"Push force direction: {forceDirection}");

        // Trigger jump SFX on the target (temporary feedback)
        if (!self) {
            PlayPushSFX(pushedCharacter);
        }  

        // Apply cooldown and stamina cost
        coolDownLeft = PUSH_COOLDOWN;
        localCharacter.UseStamina(STAMINA_COST * ((currentCharge / MAX_CHARGE) * MAX_STAMINA_COST_MULTIPLIER), true);

        // Send RPC to all clients to synchronize the push
        Plugin.Log.LogInfo("Sending Push RPC Event");
        localCharacter.view.RPC("PushPlayer_Rpc", RpcTarget.All, pushedCharacter.view.ViewID, forceDirection, localCharacter.view.ViewID);
    }

    /// <summary>
    /// Creates a solid-colored texture for UI drawing.
    /// Used for drawing colored bars in OnGUI.
    /// </summary>
    /// <param name="width">Width of the texture</param>
    /// <param name="height">Height of the texture</param>
    /// <param name="color">Color to fill the texture with</param>
    /// <returns>A fully colored Texture2D</returns>
    private Texture2D MakeTex(int width, int height, Color color) {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; ++i) pixels[i] = color;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    /// <summary>
    /// Displays UI elements:
    /// - Protection status indicator (when enabled)
    /// - Charge progress bar (when charging)
    /// </summary>
    private void OnGUI() {
        // === UI: Protection Status ===
        if (showProtectionUI && protectionPush) {
            GUIStyle style = new GUIStyle() {
                fontSize = 20,
                normal = { textColor = Color.yellow },
                fontStyle = FontStyle.Bold
            };

            string text = "Protection is enabled";
            Vector2 textSize = style.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(Screen.width - textSize.x - 10, 10, textSize.x, textSize.y), text, style);
        }

        // === UI: Charge Bar ===
        if (showChargeBar && isCharging) {
            // Initialize blank texture if not already created
            if (blankTexture is null) {
                blankTexture = MakeTex(1, 1, Color.white);
            }

            float barWidth = 200f;
            float barHeight = 20f;
            float progress = currentCharge / MAX_CHARGE;

            float x = (Screen.width - barWidth) / 2f;
            float y = Screen.height * 0.8f;

            // Draw background (gray)
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), blankTexture, ScaleMode.StretchToFill, false, 0, Color.grey, 0, 0);

            // Draw progress (colored)
            GUI.DrawTexture(new Rect(x, y, barWidth * progress, barHeight), blankTexture, ScaleMode.StretchToFill, false, 0, chargeBarColor, 0, 0);

            // Draw charge label above the bar
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.black }
            };
            GUI.Label(new Rect(x, y, barWidth, 20), $"Push Charge: {(progress * 100):F0}%", textStyle);
        }
    }

    /// <summary>
    /// Recursively searches for a Character component on the given GameObject or any of its parents.
    /// Useful for raycast hits that may not directly hit the root character object.
    /// </summary>
    /// <param name="obj">The GameObject to start searching from</param>
    /// <returns>The Character component if found; otherwise null</returns>
    private Character? GetCharacter(GameObject? obj) {
        if (obj is null) return null;

        // Check current object first
        if (obj.TryGetComponent(out Character character))
            return character;

        // Traverse up the hierarchy
        Transform? parent = obj.transform.parent;
        if (parent is not null) {
            return GetCharacter(parent.gameObject);
        }

        // No Character found in hierarchy
        return null;
    }

    /// <summary>
    /// Plays the push animation on the specified character.
    /// Uses the character's animator to trigger the reach animation.
    /// </summary>
    /// <param name="character">The character to animate</param>
    private void PlayPushAnimation(Character? character) {
        if (character is null) return;
        CharacterAnimations? charAnims = character.GetComponent<CharacterAnimations>();
        if (charAnims is null) return;
        Animator? animator = charAnims.character.refs.animator;
        animator?.Play("A_Scout_Reach_Straight");
    }

    /// <summary>
    /// Activates the jump sound effect on the target character.
    /// Used as audio feedback when a push is applied.
    /// </summary>
    /// <param name="character">The character to play SFX on</param>
    private void PlayPushSFX(Character character) {
        Transform sfx = character.gameObject.transform.Find("Scout").Find("SFX").Find("Movement").Find("SFX Jump");
        if (sfx is null) {
            Plugin.Log.LogError("Could not find sound effect for pushed character.");
        }
        else {
            sfx.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// RPC callback to apply a push force to a specific character.
    /// Called on all clients to ensure synchronized behavior.
    /// </summary>
    /// <param name="viewID">Photon View ID of the target character</param>
    /// <param name="force">Force vector to apply</param>
    /// <param name="senderID">Photon View ID of the pushing character</param>
    [PunRPC]
    private void PushPlayer_Rpc(int viewID, Vector3 force, int senderID) {
        Plugin.Log.LogInfo($"Received Push RPC Event for ID: {viewID}, Force: {force}, from SenderID: {senderID}");

        // Trigger push animation on the sender (if visible on this client)
        if (Character.GetCharacterWithPhotonID(senderID, out Character senderCharacter)) {
            if (senderCharacter.TryGetComponent<PushManager>(out var senderPushManager)) {
                senderPushManager.animationCoolDown = ANIMATION_TIME;
            }
        }
        else {
            Plugin.Log.LogWarning($"Could not find character with photon ID: {senderID}");
        }
        // Block push if protection mode is active
        if (protectionPush) {
            Plugin.Log.LogInfo("Push blocked by protection mode.");
            return;
        }

        // Ensure local character is identified
        // Ensure we have a reference to the local character
        if (localCharacter is null) {
            localCharacter = Character.AllCharacters.First(c => c.IsLocal);
            if (localCharacter is null) {
                Plugin.Log.LogError("Failed to find local character in PushPlayer_Rpc.");
                return;
            }
        }

        // Only apply force if this client controls the target character
        int localViewID = localCharacter.view.ViewID;
        if (viewID != localViewID) {
            Plugin.Log.LogInfo($"Local Player ID: {localViewID} is not the pushed ID: {viewID}");
            return;
        }

        // Play SFX locally for feedback
        PlayPushSFX(localCharacter);

        // Apply physical force to the character
        localCharacter.AddForce(force);
    }
}
