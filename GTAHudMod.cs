using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppSLZ.Marrow;
using BoneLib.BoneMenu;
using LabFusion.SDK.Points;

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

                clockText.text = DateTime.Now.ToString("h:mm tt");

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

            GameObject fistGO = new GameObject("IconImage");
            fistGO.transform.SetParent(iconGO.transform, false);
            Image fistImg = fistGO.AddComponent<Image>();
            fistImg.sprite = BuildEmbeddedIconSprite();
            fistImg.type = Image.Type.Simple;
            fistImg.preserveAspect = true;
            RectTransform fistRect = fistImg.rectTransform;
            fistRect.anchorMin = new Vector2(0.08f, 0.08f);
            fistRect.anchorMax = new Vector2(0.92f, 0.92f);
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

        // Loads the icon from an embedded base64-encoded PNG (supplied by
        // the user) instead of procedurally drawing a shape. Decoded once
        // at HUD build time via Texture2D.LoadImage.
        private static readonly string EmbeddedIconBase64 =
            "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";

        private Sprite BuildEmbeddedIconSprite()
        {
            byte[] bytes = Convert.FromBase64String(EmbeddedIconBase64);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }

        // ----------------------------------------------------------------
        // HEALTH LOOKUP
        // ----------------------------------------------------------------
        private bool? lastHealthFound = null;

        private float GetHealthFraction()
        {
            try
            {
                healthRefreshTimer -= Time.deltaTime;
                if (cachedHealthComponent == null || healthRefreshTimer <= 0f)
                {
                    healthRefreshTimer = 2f;
                    var found = UnityEngine.Object.FindObjectOfType(
                        Il2CppInterop.Runtime.Il2CppType.From(typeof(Il2CppSLZ.Marrow.Player_Health)));

                    Il2CppSLZ.Marrow.Player_Health casted = null;
                    if (found != null)
                    {
                        casted = found.TryCast<Il2CppSLZ.Marrow.Player_Health>();
                    }
                    cachedHealthComponent = casted;

                    bool foundNow = casted != null;
                    if (lastHealthFound != foundNow)
                    {
                        lastHealthFound = foundNow;
                        if (foundNow)
                            MelonLogger.Msg("[HealthCheck] Player_Health cast succeeded.");
                        else
                            MelonLogger.Warning("[HealthCheck] Player_Health not found or cast failed right now.");
                    }
                }

                if (cachedHealthComponent is Il2CppSLZ.Marrow.Player_Health ph && ph != null)
                {
                    if (ph.max_Health <= 0f) return 1f;
                    return ph.curr_Health / ph.max_Health;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[HealthCheck] Exception reading health: " + e.Message);
            }

            return 1f;
        }

        // ----------------------------------------------------------------
        // BITS LOOKUP - confirmed via LabFusion.dll's own metadata:
        // LabFusion.SDK.Points.PointItemManager.GetBitCount() is public
        // static, no parameters, returns int.
        // ----------------------------------------------------------------
        private long GetBits()
        {
            try
            {
                return PointItemManager.GetBitCount();
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
