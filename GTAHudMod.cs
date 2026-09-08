using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppSLZ.Marrow;

[assembly: MelonInfo(typeof(GTAHudMod.Main), "GTA SA HUD", "2.0.0", "You")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace GTAHudMod
{
    // ============================================================
    // VERSION 2: World-space Canvas HUD, parented to the headset
    // camera, instead of the old OnGUI() screen overlay (which
    // doesn't render inside the VR view - it only draws to a flat
    // "mirror" window that standalone Quest doesn't have at all).
    // ============================================================
    public class Main : MelonMod
    {
        // ----------------------------------------------------------------
        // TUNABLES
        // ----------------------------------------------------------------
        // Where the HUD sits relative to your head, in meters, in the
        // camera's local space: X = right, Y = up, Z = forward.
        private static readonly Vector3 HudLocalPosition = new Vector3(0.18f, 0.12f, 0.5f);
        private const float HudScale = 0.0006f; // shrinks "pixel" sized UI to a small real-world panel

        private Color barBg = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private Color barFill = new Color(0.78f, 0.15f, 0.15f, 1f);
        private Color moneyColor = new Color(0.15f, 0.55f, 0.25f, 1f);

        private GameObject hudRoot;
        private Image healthFillImage;
        private Text clockText;
        private Text moneyText;

        private object cachedHealthComponent;
        private float healthRefreshTimer;

        public override void OnUpdate()
        {
            try
            {
                if (hudRoot == null)
                {
                    TryBuildHud();
                    if (hudRoot == null) return; // camera not ready yet, try again next frame
                }

                // Clock
                clockText.text = DateTime.Now.ToString("HH:mm");

                // Health
                float healthFraction = GetHealthFraction();
                RectTransform fillRect = healthFillImage.rectTransform;
                fillRect.anchorMax = new Vector2(Mathf.Clamp01(healthFraction), 1f);

                // Money / Bits
                long bits = GetBits();
                moneyText.text = "$" + bits.ToString("D8");
            }
            catch (Exception e)
            {
                // Log once-ish rather than spamming every frame if something's wrong
                MelonLogger.Error("GTAHudMod OnUpdate error: " + e);
                hudRoot = null; // force a rebuild attempt next frame in case the camera changed
            }
        }

        // ----------------------------------------------------------------
        // BUILD THE WORLD-SPACE HUD (once, when a camera is available)
        // ----------------------------------------------------------------
        private void TryBuildHud()
        {
            Camera cam = Camera.main;
            if (cam == null) return; // no camera yet (e.g. still on a loading screen)

            // Root canvas, parented to the camera so it always follows your view
            GameObject canvasGO = new GameObject("GTAHudMod_Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();

            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(300f, 150f);

            canvasGO.transform.SetParent(cam.transform, false);
            canvasGO.transform.localPosition = HudLocalPosition;
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = Vector3.one * HudScale;

            hudRoot = canvasGO;

            Font builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // --- Clock text ---
            GameObject clockGO = new GameObject("Clock");
            clockGO.transform.SetParent(canvasGO.transform, false);
            clockText = clockGO.AddComponent<Text>();
            clockText.font = builtinFont;
            clockText.fontSize = 28;
            clockText.fontStyle = FontStyle.Bold;
            clockText.alignment = TextAnchor.MiddleRight;
            clockText.color = Color.white;
            RectTransform clockRect = clockText.rectTransform;
            clockRect.anchorMin = new Vector2(0f, 0.6f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.offsetMin = Vector2.zero;
            clockRect.offsetMax = Vector2.zero;

            // --- Health bar background ---
            GameObject barBgGO = new GameObject("HealthBarBg");
            barBgGO.transform.SetParent(canvasGO.transform, false);
            Image barBgImage = barBgGO.AddComponent<Image>();
            barBgImage.color = barBg;
            RectTransform barBgRect = barBgImage.rectTransform;
            barBgRect.anchorMin = new Vector2(0f, 0.4f);
            barBgRect.anchorMax = new Vector2(1f, 0.55f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            // --- Health bar fill (child of background, grown via anchorMax.x) ---
            GameObject barFillGO = new GameObject("HealthBarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            healthFillImage = barFillGO.AddComponent<Image>();
            healthFillImage.color = barFill;
            RectTransform fillRectInit = healthFillImage.rectTransform;
            fillRectInit.anchorMin = new Vector2(0f, 0f);
            fillRectInit.anchorMax = new Vector2(1f, 1f); // updated live in OnUpdate
            fillRectInit.offsetMin = Vector2.zero;
            fillRectInit.offsetMax = Vector2.zero;

            // --- Money text ---
            GameObject moneyGO = new GameObject("Money");
            moneyGO.transform.SetParent(canvasGO.transform, false);
            moneyText = moneyGO.AddComponent<Text>();
            moneyText.font = builtinFont;
            moneyText.fontSize = 26;
            moneyText.fontStyle = FontStyle.Bold;
            moneyText.alignment = TextAnchor.MiddleCenter;
            moneyText.color = moneyColor;
            RectTransform moneyRect = moneyText.rectTransform;
            moneyRect.anchorMin = new Vector2(0f, 0f);
            moneyRect.anchorMax = new Vector2(1f, 0.35f);
            moneyRect.offsetMin = Vector2.zero;
            moneyRect.offsetMax = Vector2.zero;

            MelonLogger.Msg("GTAHudMod: world-space HUD built and attached to camera.");
        }

        // ----------------------------------------------------------------
        // HEALTH LOOKUP (same as before)
        // ----------------------------------------------------------------
        private float GetHealthFraction()
        {
            try
            {
                healthRefreshTimer -= Time.deltaTime;
                if (cachedHealthComponent == null || healthRefreshTimer <= 0f)
                {
                    healthRefreshTimer = 2f;
                    cachedHealthComponent = UnityEngine.Object
                        .FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.From(
                            typeof(Il2CppSLZ.Marrow.Player_Health)));
                }

                if (cachedHealthComponent is Il2CppSLZ.Marrow.Player_Health ph)
                {
                    if (ph.max_Health <= 0f) return 1f;
                    return ph.curr_Health / ph.max_Health;
                }
            }
            catch
            {
                // Field/class name likely doesn't match this game version.
            }

            return 1f;
        }

        // ----------------------------------------------------------------
        // BITS LOOKUP  <-- still a placeholder, see GTAHudMod.cs notes below
        // ----------------------------------------------------------------
        private long GetBits()
        {
            try
            {
                // PLACEHOLDER. See the notes at the bottom of this file for
                // how to find the real Bits value in your game's dump.
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}

/*
====================================================================
WHY THIS VERSION IS DIFFERENT
====================================================================
Version 1 used Unity's OnGUI() - a legacy "immediate mode" drawing
system that renders to a flat screen-space overlay. In most VR games,
including BONELAB, that overlay is never composited into the stereo
image sent to the headset lenses; it only shows up on a flat "mirror"
window on a connected PC, which a standalone headset with no PC
doesn't have at all. So the mod could load perfectly (as your log
confirmed) and still show literally nothing.

This version instead creates a real Unity UI Canvas set to
"World Space" render mode, parented directly to the headset camera's
transform. Because it's parented to the camera, it moves and rotates
with your head automatically, giving the effect of a fixed HUD
element - the same trick most VR HUD mods use.

====================================================================
LIKELY NEW BUILD REQUIREMENT: UnityEngine.UI
====================================================================
This version uses Canvas, Image, Text, and RectTransform - classes
that live in an assembly separate from the ones used before (usually
UnityEngine.UIModule.dll for Canvas/RectTransform, and UnityEngine.UI.dll
for Image/Text/Button). If the next build fails with an error like
"the type or namespace 'Canvas' could not be found" or similar for
Image/Text, go back into your Il2CppAssemblies folder and upload
whichever of these you find:
  - UnityEngine.UIModule.dll
  - UnityEngine.UI.dll
into the GitHub repo root alongside the other DLLs. No .csproj change
needed - it auto-references every .dll in the root.

====================================================================
IF THE HUD STILL DOESN'T SHOW AFTER THIS VERSION
====================================================================
A few things worth checking, in order:
1. Confirm no new exception appears in Latest.log for GTAHudMod after
   this update (same check as before).
2. The HUD position (HudLocalPosition at the top of this file) assumes
   your headset camera's local forward/right/up axes are the obvious
   ones. If it builds and loads fine but is invisible, it may be
   positioned behind you or too small/large - try changing the Z value
   (forward distance) to something larger like 1.0f, or the scale
   (HudScale) larger, as a first troubleshooting step.
3. Camera.main might not be the actual per-eye rendering camera in
   some VR setups (some rigs use a dedicated XR camera not tagged
   "MainCamera"). If nothing shows even after adjusting position/scale,
   this is the next thing to investigate - happy to help track down
   the correct camera reference if we get to that point.
*/
