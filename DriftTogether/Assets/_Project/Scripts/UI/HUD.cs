using System.Collections.Generic;
using DriftTogether.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DriftTogether.UI
{
    /// <summary>In-game HUD: hull pips, mushroom counter, context hint, subtitles.</summary>
    public sealed class HUD : MonoBehaviour
    {
        readonly List<Image> _hullPips = new List<Image>();
        Text _mushroomText;
        Text _hintText;
        Text _subtitleText;
        RectTransform _subtitlePanel;
        CanvasGroup _fadeGroup;

        float _subtitleUntil;
        readonly Queue<string> _pending = new Queue<string>();

        public static HUD Create()
        {
            var canvas = UIBuilder.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<HUD>();
            hud.BuildWidgets(canvas.transform);
            return hud;
        }

        void BuildWidgets(Transform root)
        {
            // Hull pips, top-left.
            var hullPanel = UIBuilder.CreateInvisiblePanel(root, "Hull");
            UIBuilder.Anchor(hullPanel, new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(240f, 54f));
            for (int i = 0; i < HullIntegrity.MaxPoints; i++)
            {
                var pip = UIBuilder.CreatePanel(hullPanel, $"Pip{i}", UIBuilder.Accent);
                UIBuilder.Anchor(pip, new Vector2(0f, 0.5f), new Vector2(i * 66f, 0f), new Vector2(54f, 26f));
                _hullPips.Add(pip.GetComponent<Image>());
            }
            var hullLabel = UIBuilder.CreateText(hullPanel, "HullLabel", "ПРОЧНОСТЬ", 18, UIBuilder.TextDim, TextAnchor.UpperLeft);
            UIBuilder.Anchor(hullLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, -6f), new Vector2(220f, 22f));

            // Mushroom counter, top-right.
            var shroomPanel = UIBuilder.CreatePanel(root, "Mushrooms", UIBuilder.PanelSoft);
            UIBuilder.Anchor(shroomPanel, new Vector2(1f, 1f), new Vector2(-28f, -24f), new Vector2(190f, 54f));
            _mushroomText = UIBuilder.CreateText(shroomPanel, "Count", "Грибы 0/5", 28, UIBuilder.TextMain);
            UIBuilder.Stretch(_mushroomText.rectTransform);

            // Context hint, center-bottom above subtitles.
            _hintText = UIBuilder.CreateText(root, "Hint", "", 30, new Color(1f, 0.9f, 0.6f));
            UIBuilder.Anchor(_hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 220f), new Vector2(900f, 46f));

            // Subtitle bar, bottom.
            _subtitlePanel = UIBuilder.CreatePanel(root, "SubtitlePanel", UIBuilder.PanelSoft);
            UIBuilder.Anchor(_subtitlePanel, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1100f, 88f));
            var timName = UIBuilder.CreateText(_subtitlePanel, "Name", "Тапок-Тим", 20, UIBuilder.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UIBuilder.Anchor(timName.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(300f, 24f));
            _subtitleText = UIBuilder.CreateText(_subtitlePanel, "Line", "", 28, UIBuilder.TextMain);
            UIBuilder.Stretch(_subtitleText.rectTransform);
            _subtitleText.rectTransform.offsetMin = new Vector2(18f, 8f);
            _subtitleText.rectTransform.offsetMax = new Vector2(-18f, -30f);
            _subtitlePanel.gameObject.SetActive(false);

            // Full-screen fade for respawn.
            var fade = UIBuilder.CreatePanel(root, "Fade", Color.black);
            UIBuilder.Stretch(fade);
            _fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            _fadeGroup.alpha = 0f;
            _fadeGroup.blocksRaycasts = false;
        }

        public void SetHull(int current)
        {
            for (int i = 0; i < _hullPips.Count; i++)
                _hullPips[i].color = i < current
                    ? UIBuilder.Accent
                    : new Color(0.25f, 0.3f, 0.3f, 0.7f);
        }

        /// <summary>Rebuilds the pip row for a different maximum (co-op raft = 5).</summary>
        public void SetHullMax(int max)
        {
            if (_hullPips.Count == max)
                return;
            var parent = (RectTransform)_hullPips[0].transform.parent;
            foreach (var pip in _hullPips)
                Destroy(pip.gameObject);
            _hullPips.Clear();
            for (int i = 0; i < max; i++)
            {
                var pip = UIBuilder.CreatePanel(parent, $"Pip{i}", UIBuilder.Accent);
                UIBuilder.Anchor(pip, new Vector2(0f, 0.5f), new Vector2(i * 52f, 0f), new Vector2(42f, 26f));
                _hullPips.Add(pip.GetComponent<Image>());
            }
        }

        /// <summary>Toggles the «мокрый» indicator (co-op).</summary>
        public void SetWet(bool wet)
        {
            if (_wetBadge == null)
            {
                var badge = UIBuilder.CreatePanel(transform, "WetBadge", new Color(0.25f, 0.55f, 0.8f, 0.85f));
                UIBuilder.Anchor(badge, new Vector2(0f, 1f), new Vector2(28f, -92f), new Vector2(160f, 36f));
                var label = UIBuilder.CreateText(badge, "Label", "МОКРЫЙ", 22, Color.white);
                UIBuilder.Stretch(label.rectTransform);
                _wetBadge = badge.gameObject;
            }
            _wetBadge.SetActive(wet);
        }

        GameObject _wetBadge;

        public void SetMushrooms(int count)
        {
            _mushroomText.text = $"Грибы {Mathf.Min(count, MushroomTracker.Goal)}/{MushroomTracker.Goal}";
        }

        /// <summary>Co-op has no mushrooms — the counter shows crew and food instead.</summary>
        public void ShowCrewCounter(int players, int food = -1)
        {
            _mushroomText.text = food >= 0
                ? $"Команда {players} · Еда {food}"
                : $"Команда: {players}";
        }

        public void SetHint(string hint) => _hintText.text = hint ?? "";

        public void EnqueueSubtitle(string line) => _pending.Enqueue(line);

        public float FadeAlpha
        {
            get => _fadeGroup.alpha;
            set => _fadeGroup.alpha = Mathf.Clamp01(value);
        }

        void Update()
        {
            if (_subtitlePanel.gameObject.activeSelf && Time.unscaledTime >= _subtitleUntil)
                _subtitlePanel.gameObject.SetActive(false);

            if (!_subtitlePanel.gameObject.activeSelf && _pending.Count > 0)
            {
                string line = _pending.Dequeue();
                _subtitleText.text = line;
                _subtitlePanel.gameObject.SetActive(true);
                _subtitleUntil = Time.unscaledTime + DialogueQueue.DisplayDuration(line);
            }
        }
    }
}
