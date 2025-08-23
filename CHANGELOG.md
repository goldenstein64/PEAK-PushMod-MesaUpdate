# Changelog

## Version 0.6.0
### Charge System
- Added **chargeable push** by holding down the push key.
- Longer charge = stronger push (up to a configurable maximum multiplier: x1.5).
- Visual charge indicator displayed as a progress bar at the bottom of the screen.
- Charge builds while holding the push key and is applied upon release.

### Self-Push
- Added a dedicated key to **push yourself**.
- Enables mobility control and environmental interactions.

### Push Protection Mode
- Toggleable protection against incoming pushes from other players.
- Pressing the protection key toggles immunity on/off.
- Status is visually indicated on-screen (top-right corner).

### Enhanced Configuration System
- Replaced `Config.Bind` with a centralized `ConfigurationHandler`.
- All key parameters are now defined as constants for easy balancing:
  - Push range (default: 2.5m)
  - Cooldown (default: 1s)
  - Stamina cost scaling (with charge)
  - UI colors and visual settings

### Improved UI & Visual Feedback
- **Charge bar** appears during button hold — smooth progress visualization via `Texture2D`.
- **Protection status indicator** shows "ON/OFF" in yellow text at top-right.
- Both UI elements are clean, non-intrusive, and customizable.

### Performance & Stability Optimizations
- Cached critical components (`Character`, `Camera.main`) in `Awake()` — eliminates per-frame `GetComponent` calls.
- Null checks during initialization prevent crashes.
- Component automatically disables itself on critical errors.
- Safe `GetCharacter()` recursion with proper null handling.
- Fixed potential issues when `mainCamera` or `Character` is missing.

### Refined Push Logic
- Final push strength now scales with:
  - Presence of the "BingBong" item (x10 multiplier)
  - Charge level (up to x1.5)
- Stamina cost dynamically scales with charge (up to x3 at full charge).
- Raycast now uses a dedicated `LayerMask` filtering only the "Character" layer — improves accuracy and performance.

### RPC & Animation Improvements
- Push animation now plays on the **sender** (not just the target).
- Push sound effect plays locally for both the attacker and the target.
- Better feedback and synchronization across network.

### Partial Localization Support
- "BingBong" item detection now uses `ItemTags.BingBong` instead of string comparison.
- Ensures compatibility across language changes and prevents name-based bugs.

## ⚙️ Configurable Settings (via PEAK/BepInEx/config/com.github.goldenstein64.PushMod_MesaUpdate.cfg)
The following options can be customized in the BepInEx configuration file:
- `PushKey` — Key to perform a regular push (default: F)
- `SelfPushKey` — Key to push yourself (default: G)
- `ProtectionKey` — Key to toggle push protection (default: F10)
- `CanCharge` — Enable or disable charge system (default: true)


## Version 0.5.3
- Characters can push themselves if they don't find a character to push

## Version 0.5.2
- Fix scout animation

## Version 0.5.1
- Forked from original project
- Updated mod to support the Mesa update
- Bing Bong Push no longer uses up all stamina (because it's funny this way)

## Version 0.5.0
- Added Bing Bong Force Multiplier
   - Holding Bing Bong while pushing will push with 10x Force
   - It also uses up the entirety of your stamina

## Version 0.4.1
- Removed log spam

## Version 0.4.0
- Fixed an issue where animations for other players would play when you were pushing then

## Version 0.3.0
- Added Animation for pushing
- Fixed the push cooldown to only be set when a player is actually interacted with
- Fixed an issue where players could push while climbing
- Fixed an issue where players could push while holding items

## Version 0.2.0
- Fixed an issue where players could push while dead or unconscious
- Fixed an issue where players could push while being carried

## Version 0.1.0
- Release