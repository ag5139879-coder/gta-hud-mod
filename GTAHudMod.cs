using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppSLZ.Marrow;

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

        public override void OnUpdate()
        {
            try
            {
                if (hudRoot == null)
                {
                    TryBuildHud();
                    if (hudRoot == null) return;
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
            BuildAbstractFist(iconGO.transform);

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

        // Simple abstract fist made of layered black shapes on the white
        // rounded icon backdrop. This is an original abstract icon, not a
        // reproduction of any specific game's artwork.
        private void BuildAbstractFist(Transform parent)
        {
            Color knuckleColor = new Color(0.08f, 0.08f, 0.08f, 1f);

            // Palm / main mass
            AddShape(parent, "Palm", new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), 0f, knuckleColor);
            // Four knuckle bumps along the top
            AddShape(parent, "Knuckle1", new Vector2(0.28f, 0.72f), new Vector2(0.16f, 0.28f), -10f, knuckleColor);
            AddShape(parent, "Knuckle2", new Vector2(0.42f, 0.78f), new Vector2(0.16f, 0.32f), -3f, knuckleColor);
            AddShape(parent, "Knuckle3", new Vector2(0.58f, 0.78f), new Vector2(0.16f, 0.32f), 3f, knuckleColor);
            AddShape(parent, "Knuckle4", new Vector2(0.72f, 0.72f), new Vector2(0.16f, 0.28f), 10f, knuckleColor);
            // Thumb
            AddShape(parent, "Thumb", new Vector2(0.22f, 0.38f), new Vector2(0.14f, 0.22f), -35f, knuckleColor);
        }

        private void AddShape(Transform parent, string name, Vector2 anchorCenter, Vector2 size, float rotation, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.sizeDelta = new Vector2(size.x * 100f, size.y * 100f);
            rt.localRotation = Quaternion.Euler(0f, 0f, rotation);
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
STYLE NOTES
====================================================================
- Outline component (UnityEngine.UI.Outline) gives the thick blocky
  black stroke around text, applied twice at opposite offsets so it
  reads as a full outline rather than a single drop shadow.
- Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd") is a
  built-in Unity sprite with rounded corners and a 9-sliced border,
  used here for the icon and money-pill backgrounds without needing
  any external image files.
- The fist icon is built from six simple rotated rounded-rect shapes
  layered together (a palm block, four knuckle bumps, one thumb) to
  abstractly suggest a fist silhouette. It won't be a precise match to
  the reference image's hand-drawn artwork - that art is a specific
  copyrighted asset I can't reproduce - but it should read as a similar
  fist-shaped icon in the same white rounded box.
*/
