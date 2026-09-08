using System;
using MelonLoader;
using UnityEngine;
// NOTE: this using will only resolve once you've added a reference to the
// IL2CPP-interop-generated assembly that contains SLZ.Marrow types (found in
// BONELAB\MelonLoader\Il2CppAssemblies\ after running the game once with ML
// installed). If Player_Health isn't in there under that exact name, see the
// "HOW TO FIND THE REAL HEALTH FIELDS" notes at the bottom of this file.
using Il2CppSLZ.Marrow;

[assembly: MelonInfo(typeof(GTAHudMod.Main), "GTA SA HUD", "1.0.0", "You")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace GTAHudMod
{
    public class Main : MelonMod
    {
        // ----------------------------------------------------------------
        // TUNABLES
        // ----------------------------------------------------------------
        private const float PadRight = 20f;
        private const float PadTop = 20f;
        private const float IconSize = 70f;
        private const float BarWidth = 260f;
        private const float BarHeight = 14f;

        private Color barBg = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private Color barFill = new Color(0.78f, 0.15f, 0.15f, 1f);   // GTA SA red health bar
        private Color moneyColor = new Color(0.15f, 0.55f, 0.25f, 1f); // GTA SA green cash

        private GUIStyle clockStyle;
        private GUIStyle moneyStyle;
        private Texture2D pixel;
        private bool stylesReady = false;

        // Cached reference to the player's health component, refreshed
        // periodically in case the player object gets recreated on level load.
        private object cachedHealthComponent;
        private float healthRefreshTimer;

        public override void OnGUI()
        {
            EnsureStyles();

            float screenW = Screen.width;
            float x = screenW - PadRight - BarWidth;
            float y = PadTop;

            // --- Fist icon (placeholder box; see notes below for a real sprite) ---
            Rect iconRect = new Rect(x, y, IconSize, IconSize);
            DrawRect(iconRect, Color.white);
            GUI.Box(iconRect, "\u270A"); // fist emoji fallback if your font supports it

            // --- Clock (real system time) ---
            string clock = DateTime.Now.ToString("HH:mm");
            Rect clockRect = new Rect(x + IconSize + 12f, y, BarWidth - IconSize - 12f, IconSize * 0.7f);
            GUI.Label(clockRect, clock, clockStyle);

            // --- Health bar ---
            float healthFraction = GetHealthFraction();
            Rect barRect = new Rect(x, y + IconSize + 8f, BarWidth, BarHeight);
            DrawRect(barRect, barBg);
            Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(healthFraction), barRect.height);
            DrawRect(fillRect, barFill);

            // --- Money / Bits ---
            long bits = GetBits();
            string moneyText = "$" + bits.ToString("D8"); // zero-padded like GTA SA's $00250350
            Rect moneyRect = new Rect(x, barRect.y + BarHeight + 6f, BarWidth, 34f);
            DrawRect(moneyRect, new Color(0, 0, 0, 0.6f));
            GUI.Label(moneyRect, moneyText, moneyStyle);
        }

        // ----------------------------------------------------------------
        // HEALTH LOOKUP
        // ----------------------------------------------------------------
        // Best-effort based on the commonly referenced Bonelab component
        // "Player_Health" (Il2CppSLZ.Marrow.Player_Health) with curr_Health /
        // max_Health fields. Verify against your own game version's dump --
        // see notes at the bottom of the file if this comes back empty.
        private float GetHealthFraction()
        {
            try
            {
                healthRefreshTimer -= Time.deltaTime;
                if (cachedHealthComponent == null || healthRefreshTimer <= 0f)
                {
                    healthRefreshTimer = 2f; // re-scan every 2s in case of level reload
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
                // See "HOW TO FIND THE REAL HEALTH FIELDS" below.
            }

            return 1f; // fallback so the bar doesn't look broken while you debug
        }

        // ----------------------------------------------------------------
        // BITS LOOKUP  <-- fill this in, see instructions at bottom of file
        // ----------------------------------------------------------------
        private long GetBits()
        {
            try
            {
                // PLACEHOLDER. Bonelab stores your Bits balance somewhere in its
                // save/economy system, but the exact class/method name shifts
                // between game patches, so I can't hardcode it reliably here.
                // See the "HOW TO FIND THE REAL BITS VALUE" notes below for a
                // 2-minute way to find and drop in the correct call.
                //
                // Example shape once you find it (illustrative only):
                // return (long)SLZ.Marrow.SomeEconomyClass.Instance.CurrentBits;

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------
        private void EnsureStyles()
        {
            if (stylesReady) return;

            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            clockStyle.normal.textColor = Color.white;

            moneyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            moneyStyle.normal.textColor = moneyColor;

            stylesReady = true;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }
    }
}

/*
====================================================================
HOW TO FIND THE REAL HEALTH FIELDS (if Player_Health doesn't work)
====================================================================
Same tool as below (dnSpy/ILSpy on BONELAB\MelonLoader\Il2CppAssemblies\).
Search for "Health" instead of "Bits". You're looking for a component
attached to the player rig with something like curr_Health/max_Health,
CurrentHealth/MaxHealth, or a HealthController-style class. Once found,
swap the type name and field names in GetHealthFraction() above to match
exactly (case-sensitive).

If FindObjectOfType turns up nothing at all, the component may only
exist on a child object of the player rig manager rather than being
easily discoverable game-wide -- in that case grab it via the rig
manager singleton instead (search the dump for "RigManager") and walk
down to the health component from there.

====================================================================
HOW TO FIND THE REAL BITS VALUE (once, ~2 minutes)
====================================================================
Bonelab is an IL2CPP game, so MelonLoader auto-generates readable C#
stub assemblies the first time you run the game with it installed,
at:
    BONELAB\MelonLoader\Il2CppAssemblies\

1. Open that folder in a .NET decompiler-friendly tool (dnSpy, ILSpy,
   or just `dotnet-ilspycmd`) and load the SLZ.Marrow / game assemblies.
2. Search (Ctrl+Shift+F in dnSpy) for "Bits" — Bonelab's currency is
   called Bits in its UI, so the backing field/property/class usually
   contains that word (e.g. something like an "EconomyUtilities",
   "BitsWallet", or a save-data field on the player's save profile).
3. Note the fully-qualified class name and whether it's a static
   singleton (Instance) or something you fetch via
   GameObject.FindObjectOfType<T>().
4. Replace the body of GetBits() above with the real call, e.g.:

    return (long)Il2CppSLZ.Marrow.SomeClass.Instance.bits;

If you'd rather not dig through the dump yourself, paste me the
relevant class definition you find (or just the search results for
"Bits") and I'll wire up the exact call for you.

====================================================================
BUILD INSTRUCTIONS
====================================================================
1. Install MelonLoader 0.6.6+ onto your BONELAB install and launch the
   game once so it generates Il2CppAssemblies.
2. Create a new C# Class Library (.NET 6) project in Visual Studio.
3. Add references to:
     - MelonLoader.dll                (BONELAB\MelonLoader\)
     - 0Harmony.dll                   (BONELAB\MelonLoader\)
     - UnityEngine.CoreModule.dll     (BONELAB\MelonLoader\Managed\ or
                                        Il2CppAssemblies\)
     - UnityEngine.IMGUIModule.dll    (same folder)
     - Il2Cppmscorlib.dll and the SLZ.Marrow IL2CPP-interop assembly
       (BONELAB\MelonLoader\Il2CppAssemblies\) that contains Player_Health
     - Il2CppInterop.Runtime.dll     (BONELAB\MelonLoader\net6\ or similar,
                                       depending on your ML version)
   (Exact managed-assembly folder name can vary slightly by ML version;
   look inside your BONELAB\MelonLoader folder for the ones present.)
4. Paste this file in, build, and drop the resulting .dll into
   BONELAB\Mods\.
5. Launch the game — the HUD should appear top-right.

Notes on the visuals: I built this with plain drawn rectangles rather
than the real GTA: San Andreas HUD art, since those are Rockstar's
copyrighted assets and I can't reproduce them. The layout (fist icon
slot, red bar, green cash readout) mimics the composition though —
if you want, drop your own PNG icon/font assets into the mod and I can
show you how to load and draw those textures instead of the plain
rectangles for a closer match.
*/
