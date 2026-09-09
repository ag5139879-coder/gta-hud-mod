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
