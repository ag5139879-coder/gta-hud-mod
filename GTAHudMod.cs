using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppSLZ.Marrow;
using BoneLib.BoneMenu;

[assembly: MelonInfo(typeof(GTAHudMod.Main), "GTA SA HUD", "2.1.0", "You")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace GTAHudMod
{
    public class Main : MelonMod
    {
        // ----------------------------------------------------------------
        // TUNABLES
        // ----------------------------------------------------------------
        private static readonly Vector3 HudLocalPosition = new Vector3(0.18f, 0.12f, 0.5f);
        private const float HudScale = 0.0006f;

        private Color barBg = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        private Color barFill = new Color(0.80f, 0.13f, 0.13f, 1f);
        private Color moneyGreen = new Color(0.20f, 0.55f, 0.25f, 1f);
        private Color moneyBg = new Color(0f, 0f, 0f, 0.55f);

        private GameObject hudRoot;
        private Image healthFillImage;
        private Text clockText;
        private Text moneyText;

        private object cachedHealthComponent;
        private float healthRefreshTimer;

        private bool hudEnabled = true;

        // ----------------------------------------------------------------
        // BONEMENU TOGGLE - runs once at mod startup
        // ----------------------------------------------------------------
        public override void OnInitializeMelon()
        {
            try
            {
                Page page = Page.Root.CreatePage("GTA HUD", Color.white);
                page.CreateBool("HUD Enabled", Color.white, hudEnabled, (value) =>
                {
                    hudEnabled = value;
                    if (hudRoot != null) hudRoot.SetActive(hudEnabled);
                });
                MelonLogger.Msg("GTAHudMod: BoneMenu toggle created under 'GTA HUD'.");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("GTAHudMod: could not create BoneMenu toggle (is BoneLib installed? API mismatch?): " + e.Message);
            }
        }

        public override void OnUpdate()
        {
            try
            {
                if (!hudEnabled)
                {
                    if (hudRoot != null) hudRoot.SetActive(false);
                    return;
                }

                if (hudRoot == null)
                {
                    TryBuildHud();
                    if (hudRoot == null) return;
                }
                else if (!hudRoot.activeSelf)
                {
                    hudRoot.SetActive(true);
                }

                clockText.text = DateTime.Now.ToString("HH:mm");

                float healthFraction = GetHealthFraction();
                healthFillImage.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(healthFraction), 1f);

                long bits = GetBits();
                moneyText.text = "$" + bits.ToString("D8");
            }
            catch (Exception e)
            {
                MelonLogger.Error("GTAHudMod OnUpdate error: " + e);
                hudRoot = null;
            }
        }

        // ----------------------------------------------------------------
        // BUILD HUD - laid out to match the GTA SA reference:
        //   [ fist icon ]   [ clock, right-aligned ]
        //   [        health bar, full width        ]
        //   [       money pill, centered            ]
        // ----------------------------------------------------------------
        private void TryBuildHud()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Sprite roundedSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            Font builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject canvasGO = new GameObject("GTAHudMod_Canvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();

            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(340f, 170f);

            canvasGO.transform.SetParent(cam.transform, false);
            canvasGO.transform.localPosition = HudLocalPosition;
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = Vector3.one * HudScale;
            hudRoot = canvasGO;

            // ---------- Row 1 (top): icon (left) + clock (right) ----------
            GameObject iconGO = new GameObject("IconBg");
            iconGO.transform.SetParent(canvasGO.transform, false);
            Image iconBg = iconGO.AddComponent<Image>();
            iconBg.sprite = roundedSprite;
            iconBg.type = Image.Type.Sliced;
            iconBg.color = Color.white;
            RectTransform iconRect = iconBg.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.62f);
            iconRect.anchorMax = new Vector2(0.32f, 1f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            GameObject fistGO = new GameObject("FistIcon");
            fistGO.transform.SetParent(iconGO.transform, false);
            Image fistImg = fistGO.AddComponent<Image>();
            fistImg.sprite = BuildFistSprite();
            fistImg.type = Image.Type.Simple;
            fistImg.preserveAspect = true;
            RectTransform fistRect = fistImg.rectTransform;
            fistRect.anchorMin = new Vector2(0.12f, 0.06f);
            fistRect.anchorMax = new Vector2(0.88f, 0.94f);
            fistRect.offsetMin = Vector2.zero;
            fistRect.offsetMax = Vector2.zero;

            GameObject clockGO = new GameObject("Clock");
            clockGO.transform.SetParent(canvasGO.transform, false);
            clockText = clockGO.AddComponent<Text>();
            clockText.font = builtinFont;
            clockText.fontSize = 30;
            clockText.fontStyle = FontStyle.Bold;
            clockText.alignment = TextAnchor.MiddleRight;
            clockText.color = Color.white;
            AddOutline(clockGO);
            RectTransform clockRect = clockText.rectTransform;
            clockRect.anchorMin = new Vector2(0.36f, 0.62f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.offsetMin = Vector2.zero;
            clockRect.offsetMax = Vector2.zero;

            // ---------- Row 2: health bar, full width ----------
            GameObject barBgGO = new GameObject("HealthBarBg");
            barBgGO.transform.SetParent(canvasGO.transform, false);
            Image barBgImage = barBgGO.AddComponent<Image>();
            barBgImage.color = barBg;
            RectTransform barBgRect = barBgImage.rectTransform;
            barBgRect.anchorMin = new Vector2(0f, 0.46f);
            barBgRect.anchorMax = new Vector2(1f, 0.58f);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            GameObject barFillGO = new GameObject("HealthBarFill");
            barFillGO.transform.SetParent(barBgGO.transform, false);
            healthFillImage = barFillGO.AddComponent<Image>();
            healthFillImage.color = barFill;
            RectTransform fillRectInit = healthFillImage.rectTransform;
            fillRectInit.anchorMin = new Vector2(0f, 0f);
            fillRectInit.anchorMax = new Vector2(1f, 1f);
            fillRectInit.offsetMin = Vector2.zero;
            fillRectInit.offsetMax = Vector2.zero;

            // ---------- Row 3: money pill, centered ----------
            GameObject moneyBgGO = new GameObject("MoneyBg");
            moneyBgGO.transform.SetParent(canvasGO.transform, false);
            Image moneyBgImage = moneyBgGO.AddComponent<Image>();
            moneyBgImage.sprite = roundedSprite;
            moneyBgImage.type = Image.Type.Sliced;
            moneyBgImage.color = moneyBg;
            RectTransform moneyBgRect = moneyBgImage.rectTransform;
            moneyBgRect.anchorMin = new Vector2(0.08f, 0.06f);
            moneyBgRect.anchorMax = new Vector2(0.92f, 0.38f);
            moneyBgRect.offsetMin = Vector2.zero;
            moneyBgRect.offsetMax = Vector2.zero;

            GameObject moneyGO = new GameObject("Money");
            moneyGO.transform.SetParent(moneyBgGO.transform, false);
            moneyText = moneyGO.AddComponent<Text>();
            moneyText.font = builtinFont;
            moneyText.fontSize = 28;
            moneyText.fontStyle = FontStyle.Bold;
            moneyText.alignment = TextAnchor.MiddleCenter;
            moneyText.color = moneyGreen;
            AddOutline(moneyGO);
            RectTransform moneyRect = moneyText.rectTransform;
            moneyRect.anchorMin = Vector2.zero;
            moneyRect.anchorMax = Vector2.one;
            moneyRect.offsetMin = Vector2.zero;
            moneyRect.offsetMax = Vector2.zero;

            MelonLogger.Msg("GTAHudMod: styled world-space HUD built.");
        }

        // Thick black outline on text, matching the blocky GTA SA font look
        private void AddOutline(GameObject textGO)
        {
            Outline outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            Outline outline2 = textGO.AddComponent<Outline>();
            outline2.effectColor = Color.black;
            outline2.effectDistance = new Vector2(-2f, 2f);
        }

        // Builds a small pixel-art fist silhouette directly onto a texture
        // at runtime (wrist/cuff, rounded knuckle mass with three notches
        // separating four knuckles, and a thumb) - drawn flat with no
        // rotation, so it stays crisp instead of the jagged sliced-sprite
        // artifacts rotation caused. This is an original abstract icon,
        // not a reproduction of any specific game's artwork.
        private Sprite BuildFistSprite()
        {
            const int size = 48;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color black = Color.black;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool filled = false;

                    // Angled wrist/forearm, entering from the bottom-right
                    int wristShift = (15 - y) / 2;
                    int wristStart = 20 + Mathf.Max(0, wristShift);
                    int wristEnd = wristStart + 20;
                    if (y <= 15 && x >= wristStart && x <= wristEnd) filled = true;

                    // Main knuckle mass, rounded top corners
                    if (!filled && x >= 8 && x <= 39 && y >= 14 && y <= 43)
                    {
                        filled = true;
                        const int cornerR = 7;
                        if (y >= 43 - cornerR)
                        {
                            if (x < 8 + cornerR)
                            {
                                int dx = (8 + cornerR) - x;
                                int dy = y - (43 - cornerR);
                                if (dx * dx + dy * dy > cornerR * cornerR) filled = false;
                            }
                            else if (x > 39 - cornerR)
                            {
                                int dx = x - (39 - cornerR);
                                int dy = y - (43 - cornerR);
                                if (dx * dx + dy * dy > cornerR * cornerR) filled = false;
                            }
                        }
                    }

                    // Knuckle notches along the top edge (rounded gaps between 4 knuckles)
                    if (filled && y >= 37)
                    {
                        int[] notchCenters = { 17, 24, 31 };
                        foreach (int nc in notchCenters)
                        {
                            int dx2 = x - nc;
                            int dy2 = y - 44;
                            if (dx2 * dx2 + dy2 * dy2 <= 8) { filled = false; break; }
                        }
                    }

                    // Finger crease lines - thin gaps running down from each notch,
                    // giving visible finger separation instead of a solid blob
                    if (filled && y >= 20 && y <= 36)
                    {
                        int[] creaseX = { 17, 24, 31 };
                        foreach (int cx in creaseX)
                        {
                            if (x == cx || x == cx + 1) { filled = false; break; }
                        }
                    }

                    // Thumb, wrapping the lower-left side
                    if (!filled && x >= 1 && x <= 11 && y >= 16 && y <= 27)
                    {
                        int dx3 = x - 6;
                        int dy3 = y - 21;
                        if (dx3 * dx3 * 2 + dy3 * dy3 <= 42) filled = true;
                    }

                    tex.SetPixel(x, y, filled ? black : clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ----------------------------------------------------------------
        // HEALTH LOOKUP
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
            catch { }

            return 1f;
        }

        // ----------------------------------------------------------------
        // BITS LOOKUP - still a placeholder, see notes at bottom of file
        // ----------------------------------------------------------------
        private long GetBits()
        {
            try
            {
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
BONEMENU TOGGLE - NEW REFERENCE NEEDED: BoneLib.dll
====================================================================
This version adds an in-game menu toggle via BoneLib's BoneMenu system
(look for a "GTA HUD" page in your BoneMenu with an "HUD Enabled"
checkbox). This needs a new reference: BoneLib.dll.

Unlike the other DLLs so far, BoneLib.dll is NOT in your
Il2CppAssemblies folder - it's a mod itself, so look for it directly
in your LemonLoader/Mods folder (you likely already have it installed,
since many Bonelab mods depend on it). Upload that BoneLib.dll into
the GitHub repo root same as always.

IMPORTANT CAVEAT: I wrote this BoneMenu code from memory of BoneLib's
typical API pattern (Page.Root.CreatePage(...) / page.CreateBool(...)),
but I couldn't verify the exact method names/signatures for your
specific BoneLib version the way I could confirm other things via a
build error. If the next build fails with errors pointing at
Page/CreatePage/CreateBool, send me that log and we'll correct the
exact API the same way we fixed everything else - by reading what the
compiler says is actually there.

Also note: the toggle code is wrapped in a try/catch, so even if the
BoneMenu API is wrong, it should log a warning rather than crash the
whole mod - worth checking Latest.log for a
"GTAHudMod: could not create BoneMenu toggle" warning if the menu
option just doesn't appear in-game after a successful build.

====================================================================
STYLE NOTES
====================================================================
- Outline component (UnityEngine.UI.Outline) gives the thick blocky
  black stroke around text, applied twice at opposite offsets so it
  reads as a full outline rather than a single drop shadow.
- Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd") is a
  built-in Unity sprite with rounded corners and a 9-sliced border,
  used here for the icon and money-pill backgrounds without needing
  any external image files.
- The fist icon is drawn as a small pixel-art texture at runtime
  (wrist/cuff, rounded knuckle mass with three notches, one thumb) -
  an original abstract icon, not a reproduction of any specific game's
  artwork.
*/
