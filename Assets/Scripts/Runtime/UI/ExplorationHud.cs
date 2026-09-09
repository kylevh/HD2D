using KVH.Game.Flow;
using KVH.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // Locked live exploration HUD: ClassicParty layout, terminal placeholder chrome.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ExplorationHud : MonoBehaviour
    {
        // Reference-resolution px from canvas edges (scaler maps to other displays).
        const float EdgeMargin = 32f;

        [SerializeField] PlayerVitals vitals;
        [SerializeField] GameFlow flow;
        [SerializeField] bool demoPulseHp;

        RectTransform _root;
        Text _name;
        Text _hpLabel;
        Text _mpLabel;
        Image _hpFill;
        Image _mpFill;
        bool _built;
        float _demoT;

        // Skip Awake — StretchFill/sizeDelta during Awake trips Unity's SendMessage guard.
        void OnEnable()
        {
            EnsureBuilt();
            if (_root != null)
            {
                PixelUiBuild.RefreshChrome(_root);
                RebindBarColors();
            }
            if (vitals == null)
                vitals = FindAnyObjectByType<PlayerVitals>();
            if (flow == null)
                flow = FindAnyObjectByType<GameFlow>();
        }

        void Start() => EnsureBuilt();

        void LateUpdate()
        {
            EnsureBuilt();
            if (vitals == null)
                vitals = FindAnyObjectByType<PlayerVitals>();
            if (flow == null)
                flow = FindAnyObjectByType<GameFlow>();

            if (_root != null)
            {
                var show = flow == null || flow.Mode != GameMode.Menu;
                if (_root.gameObject.activeSelf != show)
                    _root.gameObject.SetActive(show);
                if (!show)
                    return;
            }

            if (vitals == null)
            {
                // Still draw placeholder numbers so the lab HUD is never "invisible".
                Refresh("HERO", 1f, 1f, 100, 100, 40, 40);
                return;
            }

            if (demoPulseHp)
            {
                _demoT += Time.deltaTime;
                // gentle breathe so the bar reads as alive in the lab
                var n = 0.72f + 0.08f * Mathf.Sin(_demoT * 1.2f);
                Refresh(vitals.DisplayName, n, vitals.MpNormalized, Mathf.RoundToInt(n * vitals.MaxHp), vitals.MaxHp, vitals.Mp, vitals.MaxMp);
                return;
            }

            Refresh(vitals.DisplayName, vitals.HpNormalized, vitals.MpNormalized, vitals.Hp, vitals.MaxHp, vitals.Mp, vitals.MaxMp);
        }

        void Refresh(string name, float hpN, float mpN, int hp, int maxHp, int mp, int maxMp)
        {
            if (_name != null)
                _name.text = name;
            if (_hpFill != null)
                _hpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(hpN), 1f);
            if (_mpFill != null)
                _mpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(mpN), 1f);
            if (_hpLabel != null)
                _hpLabel.text = $"HP  {hp}/{maxHp}";
            if (_mpLabel != null)
                _mpLabel.text = $"MP  {mp}/{maxMp}";
        }

        void EnsureBuilt()
        {
            if (_built && _root != null)
            {
                // Repair stretch if something reintroduced default sizeDelta (100,100).
                UiRoot.EnsureStretchFill(_root);
                ApplyEdgeMargins();
                return;
            }

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return;

            GetComponentInParent<UiRoot>()?.ApplyBestPracticeLayout();

            // Prefer Hud folder under the canvas (must be a RectTransform).
            RectTransform parent = null;
            var hudTf = transform.Find("Hud") ?? canvas.transform.Find("Hud");
            if (hudTf is RectTransform hudRt)
                parent = hudRt;
            if (parent == null)
                parent = canvas.transform as RectTransform;

            var existing = parent.Find("ExplorationHud");
            if (existing != null && Application.isPlaying && !_built)
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                _root = existing as RectTransform;
                CacheRefs(_root);
                _built = _root != null;
            }
            else
            {
                _root = PixelUiBuild.Rect("ExplorationHud", parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
                BuildVitalsPanel(_root);
                _built = true;
            }

            if (_root != null)
                UiRoot.EnsureStretchFill(_root);

            PixelUiBuild.RefreshChrome(_root);
            RebindBarColors();
            ApplyEdgeMargins();
        }

        void ApplyEdgeMargins()
        {
            if (_root == null)
                return;

            if (_root.Find("Vitals") is RectTransform vitals)
                vitals.anchoredPosition = new Vector2(EdgeMargin, -EdgeMargin);

            // drop leftover lab hint plate if an older build left one in the hierarchy
            if (_root.Find("Hints") is RectTransform hints)
                hints.gameObject.SetActive(false);
        }

        void RebindBarColors()
        {
            if (_hpFill != null)
                _hpFill.color = PixelUiArt.Hp;
            if (_mpFill != null)
                _mpFill.color = PixelUiArt.Mp;
        }

        void BuildVitalsPanel(RectTransform parent)
        {
            // Top-left party plate — ClassicParty
            var plate = PixelUiBuild.Rect("Vitals", parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(EdgeMargin, -EdgeMargin));
            plate.sizeDelta = new Vector2(280f, 118f);
            PixelUiBuild.Panel(plate);

            _name = PixelUiBuild.Label(plate, "Name", "HERO", PixelUiArt.SizeTitle, FontStyle.Bold,
                PixelUiArt.Accent, new Vector2(16f, -14f), new Vector2(160f, 28f));
            PixelUiBuild.Label(plate, "Lv", "LV 01", PixelUiArt.SizeHint, FontStyle.Normal,
                PixelUiArt.Accent, new Vector2(180f, -16f), new Vector2(80f, 24f), TextAnchor.MiddleRight);

            _hpFill = PixelUiBuild.Bar(plate, "HpBar", PixelUiArt.Hp, new Vector2(16f, -48f),
                new Vector2(248f, 16f), out _hpLabel);
            _hpLabel.text = "HP  100/100";
            _mpFill = PixelUiBuild.Bar(plate, "MpBar", PixelUiArt.Mp, new Vector2(16f, -78f),
                new Vector2(248f, 14f), out _mpLabel);
            _mpLabel.text = "MP  40/40";
        }

        void CacheRefs(RectTransform root)
        {
            var nameT = root.Find("Vitals/Name");
            if (nameT != null)
                _name = nameT.GetComponent<Text>();
            var hpFill = root.Find("Vitals/HpBar/Fill");
            if (hpFill != null)
                _hpFill = hpFill.GetComponent<Image>();
            var mpFill = root.Find("Vitals/MpBar/Fill");
            if (mpFill != null)
                _mpFill = mpFill.GetComponent<Image>();
            var hpLab = root.Find("Vitals/HpBar/Label");
            if (hpLab != null)
                _hpLabel = hpLab.GetComponent<Text>();
            var mpLab = root.Find("Vitals/MpBar/Label");
            if (mpLab != null)
                _mpLabel = mpLab.GetComponent<Text>();

            PixelUiBuild.ApplyFont(_name, PixelUiArt.SizeTitle, FontStyle.Bold, PixelUiArt.Accent);
            PixelUiBuild.ApplyFont(_hpLabel, PixelUiArt.SizeHint, FontStyle.Bold, PixelUiArt.Cream);
            PixelUiBuild.ApplyFont(_mpLabel, PixelUiArt.SizeHint, FontStyle.Bold, PixelUiArt.Cream);
        }
    }
}
