using System.Collections.Generic;
using System.Globalization;
using _Scripts.CombatSystem;
using UnityEngine;
using UnityEngine.UI;

namespace LostArk.UI
{
    public sealed class KamenBossHpBarUI : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private HealthModule targetHealth;
        [SerializeField] private ToughnessModule targetToughness;

        [Header("Health Line")]
        [SerializeField, Min(1)] private int healthLineCount = 276;
        [SerializeField] private bool overrideFillNormalizedPerLine;
        [SerializeField, Min(0.001f)] private float fillNormalizedPerLine = 1f / 276f;
        [SerializeField] private int colorStartIndex;
        [SerializeField] private int colorStep = 1;
        [SerializeField] private bool reverseColorOrder;
        [SerializeField] private List<Color> lineColors = new List<Color>
        {
            new Color(0.8f, 0.08f, 0.18f, 0.98f),
            new Color(0.68f, 0.16f, 0.92f, 0.98f),
            new Color(0.2f, 0.56f, 1f, 0.98f),
            new Color(0.25f, 0.92f, 0.9f, 0.98f),
            new Color(1f, 0.42f, 0.11f, 0.98f),
            new Color(1f, 0.76f, 0.05f, 0.98f)
        };

        [Header("Images")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image healthLineBackgroundImage;
        [SerializeField] private Image healthDamageGhostImage;
        [SerializeField] private Image healthLineGhostImage;
        [SerializeField] private Image toughnessFillImage;
        [SerializeField] private Image iconGlowImage;

        [Header("Damage Motion")]
        [SerializeField] private bool animateHealthLine = true;
        [SerializeField, Min(0.01f)] private float healthFillSpeed = 4.5f;
        [SerializeField, Min(0f)] private float damageGhostDelay = 0.22f;
        [SerializeField, Min(0.01f)] private float damageGhostBurstDuration = 0.34f;
        [SerializeField, Min(0f)] private float damageGhostForceCollapseDelay = 0.85f;
        [SerializeField] private Color damageGhostTint = new Color(1f, 0.47f, 0.08f, 0.92f);

        [Header("Text")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text categoryText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text healthValueText;
        [SerializeField] private Text healthLineText;
        [SerializeField] private Text berserkText;
        [SerializeField] private Text raceText;
        [SerializeField] private Text regionText;

        [Header("Labels")]
        [SerializeField] private string title = "\uBE5B\uC744 \uAEBC\uB728\uB9AC\uB294 \uC790, \uCE74\uBA58";
        [SerializeField] private string category = "\uAD70\uB2E8\uC7A5";
        [SerializeField] private string level = "Lv.60";
        [SerializeField] private string berserk = "\uAD11\uD3ED\uD654\uAE4C\uC9C0";
        [SerializeField] private string race = "\uC545\uB9C8";
        [SerializeField] private string region = "\uBAA8\uB4E0 \uBA74\uC5ED";

        public HealthModule TargetHealth => targetHealth;
        public ToughnessModule TargetToughness => targetToughness;

        private float _targetHealthNormalized = 1f;
        private float _visibleHealthNormalized = 1f;
        private float _ghostHealthNormalized = 1f;
        private float _damageGhostTimer;
        private float _damageGhostLifetime;
        private float _damageGhostBurstElapsed;
        private float _damageGhostBurstStartNormalized = 1f;
        private bool _damageGhostBurstActive;
        private bool _hasHealthSample;

        private void Awake()
        {
            ApplyStaticText();
            EnsureSeparatedHealthLayers();

            if (targetHealth == null)
                targetHealth = FindBossComponent<HealthModule>();

            if (targetToughness == null)
                targetToughness = FindBossComponent<ToughnessModule>();
        }

        private void OnEnable()
        {
            SubscribeHealth(targetHealth);
            SubscribeToughness(targetToughness);
            Refresh();
        }

        private void Update()
        {
            if (!animateHealthLine || !_hasHealthSample)
                return;

            var deltaTime = Time.unscaledDeltaTime;
            _visibleHealthNormalized = MoveToward(_visibleHealthNormalized, _targetHealthNormalized, healthFillSpeed, deltaTime);

            UpdateDamageGhost(deltaTime);

            ApplyHealthLine(_visibleHealthNormalized, _ghostHealthNormalized);
        }

        private void OnDisable()
        {
            UnsubscribeHealth(targetHealth);
            UnsubscribeToughness(targetToughness);
        }

        public void SetTargets(HealthModule healthModule, ToughnessModule toughnessModule)
        {
            if (targetHealth != healthModule)
            {
                UnsubscribeHealth(targetHealth);
                targetHealth = healthModule;
                SubscribeHealth(targetHealth);
            }

            if (targetToughness != toughnessModule)
            {
                UnsubscribeToughness(targetToughness);
                targetToughness = toughnessModule;
                SubscribeToughness(targetToughness);
            }

            Refresh();
        }

        private void Refresh()
        {
            if (targetHealth != null)
                HandleHealthChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            else
                HandleHealthChanged(1f, 1f);

            if (targetToughness != null)
                HandleToughnessChanged(targetToughness.CurrentToughness, targetToughness.MaxToughness);
            else
                HandleToughnessChanged(1f, 1f);
        }

        private void SubscribeHealth(HealthModule healthModule)
        {
            if (healthModule != null)
                healthModule.OnHealthChangedUnity.AddListener(HandleHealthChanged);
        }

        private void UnsubscribeHealth(HealthModule healthModule)
        {
            if (healthModule != null)
                healthModule.OnHealthChangedUnity.RemoveListener(HandleHealthChanged);
        }

        private void SubscribeToughness(ToughnessModule toughnessModule)
        {
            if (toughnessModule != null)
                toughnessModule.OnToughnessChangedUnity.AddListener(HandleToughnessChanged);
        }

        private void UnsubscribeToughness(ToughnessModule toughnessModule)
        {
            if (toughnessModule != null)
                toughnessModule.OnToughnessChangedUnity.RemoveListener(HandleToughnessChanged);
        }

        private void HandleHealthChanged(float current, float max)
        {
            var normalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            var tookDamage = _hasHealthSample && normalized < _targetHealthNormalized - 0.0001f;
            var previousVisibleNormalized = _visibleHealthNormalized;

            _targetHealthNormalized = normalized;

            if (!_hasHealthSample || !animateHealthLine)
            {
                _visibleHealthNormalized = normalized;
                _ghostHealthNormalized = normalized;
                ResetDamageGhostMotion();
                _hasHealthSample = true;
            }
            else if (tookDamage)
            {
                _ghostHealthNormalized = Mathf.Max(_ghostHealthNormalized, previousVisibleNormalized);
                _visibleHealthNormalized = normalized;
                _damageGhostTimer = damageGhostDelay;
                _damageGhostLifetime = 0f;
                _damageGhostBurstElapsed = 0f;
                _damageGhostBurstActive = false;
                _damageGhostBurstStartNormalized = _ghostHealthNormalized;
            }
            else if (normalized > _visibleHealthNormalized)
            {
                _visibleHealthNormalized = normalized;
                _ghostHealthNormalized = normalized;
                ResetDamageGhostMotion();
            }

            ApplyHealthLine(_visibleHealthNormalized, _ghostHealthNormalized);
            ApplyHealthText(current, max);
        }

        private void HandleToughnessChanged(float current, float max)
        {
            SetFill(toughnessFillImage, max > 0f ? Mathf.Clamp01(current / max) : 0f);
        }

        private void ApplyHealthLine(float normalized, float ghostNormalized)
        {
            var maxLine = Mathf.Max(1, healthLineCount);
            var perLine = ResolveFillNormalizedPerLine(maxLine);
            if (overrideFillNormalizedPerLine)
                maxLine = Mathf.Max(1, Mathf.CeilToInt(1f / perLine));

            var line = GetLine(normalized, perLine, maxLine);
            var lineFill = GetLineFill(normalized, line, perLine);
            var targetLine = GetLine(_targetHealthNormalized, perLine, maxLine);
            var activeColor = GetLineColor(line);
            var lowerLine = Mathf.Max(0, line - 1);
            var lowerColor = GetLineColor(lowerLine);
            var hasDamageGhost = ghostNormalized > normalized + 0.0001f;
            var ghostLine = hasDamageGhost ? GetLine(ghostNormalized, perLine, maxLine) : 0;
            var ghostFill = ResolveDamageGhostFill(hasDamageGhost, ghostNormalized, ghostLine, line, lineFill, perLine);
            var ghostColorLine = ghostLine > line ? line : ghostLine;
            var ghostColor = hasDamageGhost ? ApplyDamageTint(GetLineColor(ghostColorLine)) : ClearColor(damageGhostTint);

            SetFill(healthFillImage, lineFill);
            SetFill(GetHealthLineBackgroundImage(), lowerLine > 0 ? 1f : 0f);
            SetFill(healthDamageGhostImage, hasDamageGhost ? ghostFill : 0f);
            SetColor(healthFillImage, activeColor);
            SetColor(GetHealthLineBackgroundImage(), new Color(lowerColor.r, lowerColor.g, lowerColor.b, lowerLine > 0 ? 0.98f : 0f));
            SetColor(healthDamageGhostImage, ghostColor);

            if (healthLineText != null)
            {
                healthLineText.text = "x " + targetLine.ToString(CultureInfo.InvariantCulture);
            }

            if (iconGlowImage != null)
                iconGlowImage.color = new Color(activeColor.r, activeColor.g, activeColor.b, targetLine > 0 ? 0.24f : 0.08f);
        }

        private void UpdateDamageGhost(float deltaTime)
        {
            if (!HasDamageGhost())
            {
                _ghostHealthNormalized = _visibleHealthNormalized;
                ResetDamageGhostMotion();
                return;
            }

            _damageGhostLifetime += deltaTime;

            if (_damageGhostTimer > 0f)
                _damageGhostTimer -= deltaTime;

            var forceCollapse = damageGhostForceCollapseDelay <= 0f || _damageGhostLifetime >= damageGhostForceCollapseDelay;
            if (_damageGhostTimer > 0f && !forceCollapse && !_damageGhostBurstActive)
                return;

            if (!_damageGhostBurstActive)
            {
                _damageGhostBurstActive = true;
                _damageGhostBurstElapsed = 0f;
                _damageGhostBurstStartNormalized = _ghostHealthNormalized;
            }

            _damageGhostBurstElapsed += deltaTime;

            var duration = Mathf.Max(0.01f, damageGhostBurstDuration);
            var progress = Mathf.Clamp01(_damageGhostBurstElapsed / duration);
            var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            _ghostHealthNormalized = Mathf.Lerp(_damageGhostBurstStartNormalized, _visibleHealthNormalized, easedProgress);

            if (progress >= 1f || !HasDamageGhost())
            {
                _ghostHealthNormalized = _visibleHealthNormalized;
                ResetDamageGhostMotion();
            }
        }

        private float ResolveDamageGhostFill(bool hasDamageGhost, float ghostNormalized, int ghostLine, int visibleLine, float visibleLineFill, float perLine)
        {
            if (!hasDamageGhost || visibleLine <= 0)
                return 0f;

            if (ghostLine > visibleLine)
                return 1f;

            if (ghostLine < visibleLine)
                return 0f;

            return Mathf.Max(visibleLineFill, GetLineFill(ghostNormalized, ghostLine, perLine));
        }

        private bool HasDamageGhost()
        {
            return _ghostHealthNormalized > _visibleHealthNormalized + 0.0001f;
        }

        private void ResetDamageGhostMotion()
        {
            _damageGhostTimer = 0f;
            _damageGhostLifetime = 0f;
            _damageGhostBurstElapsed = 0f;
            _damageGhostBurstStartNormalized = _ghostHealthNormalized;
            _damageGhostBurstActive = false;
        }

        private static float MoveToward(float current, float target, float speed, float deltaTime)
        {
            if (Mathf.Abs(current - target) <= 0.00001f)
                return target;

            var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * deltaTime);
            var value = Mathf.Lerp(current, target, t);
            return Mathf.Abs(value - target) <= 0.00001f ? target : value;
        }

        private static int GetLine(float normalized, float perLine, int maxLine)
        {
            return normalized > 0f ? Mathf.Clamp(Mathf.CeilToInt(normalized / perLine), 1, maxLine) : 0;
        }

        private static float GetLineFill(float normalized, int line, float perLine)
        {
            var lineStart = Mathf.Max(0, line - 1) * perLine;
            return line > 0 ? Mathf.Clamp01((normalized - lineStart) / perLine) : 0f;
        }

        private Color ApplyDamageTint(Color color)
        {
            return new Color(
                Mathf.Lerp(color.r, damageGhostTint.r, 0.58f),
                Mathf.Lerp(color.g, damageGhostTint.g, 0.58f),
                Mathf.Lerp(color.b, damageGhostTint.b, 0.58f),
                Mathf.Min(color.a, damageGhostTint.a));
        }

        private void EnsureSeparatedHealthLayers()
        {
            if (healthLineBackgroundImage == null)
                healthLineBackgroundImage = healthLineGhostImage;

            if (healthDamageGhostImage != null || healthLineBackgroundImage == null)
                return;

            var backgroundRect = healthLineBackgroundImage.rectTransform;
            var parent = backgroundRect.parent;
            if (parent == null)
                return;

            var damageGhostObject = new GameObject("HealthDamageGhost", typeof(RectTransform));
            damageGhostObject.layer = healthLineBackgroundImage.gameObject.layer;
            damageGhostObject.transform.SetParent(parent, false);

            var damageGhostRect = damageGhostObject.GetComponent<RectTransform>();
            CopyRectTransform(backgroundRect, damageGhostRect);

            healthDamageGhostImage = damageGhostObject.AddComponent<Image>();
            CopyImageSettings(healthLineBackgroundImage, healthDamageGhostImage);
            healthDamageGhostImage.raycastTarget = false;
            healthDamageGhostImage.fillAmount = 0f;
            healthDamageGhostImage.color = ClearColor(damageGhostTint);

            healthLineBackgroundImage.transform.SetSiblingIndex(0);
            healthDamageGhostImage.transform.SetSiblingIndex(1);
            if (healthFillImage != null)
                healthFillImage.transform.SetAsLastSibling();
        }

        private Image GetHealthLineBackgroundImage()
        {
            return healthLineBackgroundImage != null ? healthLineBackgroundImage : healthLineGhostImage;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.anchoredPosition = source.anchoredPosition;
            destination.sizeDelta = source.sizeDelta;
            destination.offsetMin = source.offsetMin;
            destination.offsetMax = source.offsetMax;
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        private static void CopyImageSettings(Image source, Image destination)
        {
            destination.sprite = source.sprite;
            destination.overrideSprite = source.overrideSprite;
            destination.type = source.type;
            destination.fillMethod = source.fillMethod;
            destination.fillOrigin = source.fillOrigin;
            destination.fillClockwise = source.fillClockwise;
            destination.fillCenter = source.fillCenter;
            destination.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            destination.preserveAspect = source.preserveAspect;
            destination.material = source.material;
        }

        private static Color ClearColor(Color color)
        {
            return new Color(color.r, color.g, color.b, 0f);
        }

        private float ResolveFillNormalizedPerLine(int maxLine)
        {
            if (overrideFillNormalizedPerLine)
                return Mathf.Clamp(fillNormalizedPerLine, 0.001f, 1f);

            return 1f / Mathf.Max(1, maxLine);
        }

        private Color GetLineColor(int line)
        {
            if (line <= 0)
                return new Color(0.05f, 0.05f, 0.08f, 0.9f);

            if (lineColors == null || lineColors.Count == 0)
                return Color.white;

            var orderIndex = Mathf.Max(0, line - 1);
            if (reverseColorOrder)
                orderIndex = -orderIndex;

            var step = colorStep == 0 ? 1 : colorStep;
            var rawIndex = colorStartIndex + orderIndex * step;
            return lineColors[PositiveMod(rawIndex, lineColors.Count)];
        }

        private static int PositiveMod(int value, int divisor)
        {
            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private void ApplyStaticText()
        {
            if (titleText != null) titleText.text = title;
            if (categoryText != null) categoryText.text = category;
            if (levelText != null) levelText.text = level;
            if (berserkText != null) berserkText.text = berserk;
            if (raceText != null) raceText.text = race;
            if (regionText != null) regionText.text = region;
        }

        private void ApplyHealthText(float current, float max)
        {
            if (healthValueText == null)
                return;

            healthValueText.text = FormatValue(current) + "/" + FormatValue(max);
        }

        private static void SetFill(Image image, float fill)
        {
            if (image != null)
                image.fillAmount = Mathf.Clamp01(fill);
        }

        private static void SetColor(Image image, Color color)
        {
            if (image != null)
                image.color = color;
        }

        private static string FormatValue(float value)
        {
            return Mathf.Ceil(Mathf.Max(0f, value)).ToString("N0", CultureInfo.InvariantCulture);
        }

        private static T FindBossComponent<T>() where T : Component
        {
            var boss = GameObject.Find("BossEnemy");
            if (boss != null)
            {
                var bossComponent = boss.GetComponentInChildren<T>(true);
                if (bossComponent != null)
                    return bossComponent;
            }

            foreach (var component in UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var rootName = component.transform.root.name;
                if (component.name.Contains("Boss") || rootName.Contains("Boss") || rootName.Contains("Kamen"))
                    return component;
            }

            return UnityEngine.Object.FindFirstObjectByType<T>();
        }
    }
}
