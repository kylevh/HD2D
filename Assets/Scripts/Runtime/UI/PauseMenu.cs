using KVH.Game.Flow;
using KVH.Game.Input;
using KVH.Game.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KVH.Game.UI
{
    // DualPause — left party column, right command stubs. Esc/Start toggles.
    // Display only: no inventory/status/save screens yet. Resume + Esc close.
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly string[] Commands =
        {
            "Resume", "Items", "Equipment", "Status", "Options", "Save", "Quit",
        };

        [SerializeField] InputReader input;
        [SerializeField] GameFlow flow;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerVitals vitals;

        RectTransform _root;
        Text _heroName;
        Text _heroHp;
        Image _heroHpFill;
        bool _built;
        bool _open;

        public bool IsOpen => _open;

        void OnEnable() => EnsureBuilt();
        void Start()
        {
            EnsureBuilt();
            ResolveRefs();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            ResolveRefs();
            if (input == null || !input.MenuPressed)
                return;

            // don't interrupt talk
            if (!_open && motor != null && motor.Busy == PlayerBusy.Talking)
                return;
            if (!_open && motor != null && motor.Busy != PlayerBusy.Free && motor.Busy != PlayerBusy.Menu)
                return;

            if (_open)
                Close();
            else
                Open();
        }

        public void Open()
        {
            EnsureBuilt();
            if (_root == null || _open)
                return;

            ResolveRefs();
            PixelUiBuild.RefreshChrome(_root);
            RefreshFonts();
            RefreshParty();
            _root.gameObject.SetActive(true);
            _open = true;

            if (motor != null)
                motor.Busy = PlayerBusy.Menu;
            flow?.SetMode(GameMode.Menu);
        }

        public void Close()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
            _open = false;

            if (motor != null && motor.Busy == PlayerBusy.Menu)
                motor.Busy = PlayerBusy.Free;
            flow?.SetMode(GameMode.Exploration);
        }

        void ResolveRefs()
        {
            if (input == null)
                input = FindAnyObjectByType<InputReader>();
            if (flow == null)
                flow = FindAnyObjectByType<GameFlow>();
            if (motor == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    motor = player.GetComponent<PlayerMotor>();
            }
            if (vitals == null)
                vitals = FindAnyObjectByType<PlayerVitals>();
        }

        void RefreshParty()
        {
            var name = vitals != null ? vitals.DisplayName : "HERO";
            var hp = vitals != null ? vitals.Hp : 100;
            var maxHp = vitals != null ? vitals.MaxHp : 100;
            var n = vitals != null ? vitals.HpNormalized : 1f;

            if (_heroName != null)
                _heroName.text = name;
            if (_heroHp != null)
                _heroHp.text = $"{hp}/{maxHp}";
            if (_heroHpFill != null)
                _heroHpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(n), 1f);
        }

        void EnsureBuilt()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return;

            RectTransform parent = null;
            var menus = canvas.transform.Find("Menus");
            if (menus is RectTransform menusRt)
                parent = menusRt;
            if (parent == null)
                parent = canvas.transform as RectTransform;

            if (_built && _root != null)
            {
                PixelUiBuild.RefreshChrome(_root);
                RefreshFonts();
                return;
            }

            var existing = parent.Find("PauseMenu");
            if (existing != null && Application.isPlaying && !_built)
            {
                DestroyImmediate(existing.gameObject);
                existing = null;
            }

            if (existing != null)
            {
                _root = existing as RectTransform;
                CacheRefs(_root);
                PixelUiBuild.RefreshChrome(_root);
                RefreshFonts();
                _built = _root != null;
                if (_root != null && Application.isPlaying)
                    _root.gameObject.SetActive(false);
                return;
            }

            _root = PixelUiBuild.Rect("PauseMenu", parent,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            BuildChrome(_root);
            _root.gameObject.SetActive(false);
            _built = true;
        }

        void BuildChrome(RectTransform root)
        {
            var dim = PixelUiBuild.Rect("Dim", root,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.sprite = PixelUiArt.WhiteSprite;
            dimImg.color = new Color(0f, 0f, 0f, 0.45f);
            dimImg.raycastTarget = false;

            var left = PixelUiBuild.Rect("Party", root,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(80f, 0f));
            left.sizeDelta = new Vector2(300f, 360f);
            PixelUiBuild.Panel(left);
            PixelUiBuild.Label(left, "Title", "PARTY", PixelUiArt.SizeTitle, FontStyle.Bold,
                PixelUiArt.Accent, new Vector2(20f, -20f), new Vector2(260f, 28f));

            for (var i = 0; i < 3; i++)
            {
                var row = PixelUiBuild.Rect($"Slot_{i}", left,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f, -70f - i * 90f));
                row.sizeDelta = new Vector2(260f, 80f);
                PixelUiBuild.Panel(row);

                var slotName = i == 0 ? "HERO" : $"ALLY {i}";
                var nameLabel = PixelUiBuild.Label(row, "Name", slotName, PixelUiArt.SizeBody,
                    FontStyle.Bold, PixelUiArt.Accent, new Vector2(12f, -12f), new Vector2(160f, 24f));
                var fill = PixelUiBuild.Bar(row, "Hp", PixelUiArt.Hp, new Vector2(12f, -44f),
                    new Vector2(236f, 14f), out var hp);
                hp.text = "100/100";

                if (i == 0)
                {
                    _heroName = nameLabel;
                    _heroHp = hp;
                    _heroHpFill = fill;
                }
            }

            var right = PixelUiBuild.Rect("Commands", root,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-80f, 0f));
            right.sizeDelta = new Vector2(280f, 300f);
            PixelUiBuild.Panel(right);
            PixelUiBuild.Label(right, "Title", "MENU", PixelUiArt.SizeTitle, FontStyle.Bold,
                PixelUiArt.Accent, new Vector2(20f, -20f), new Vector2(240f, 28f));

            for (var i = 0; i < Commands.Length; i++)
            {
                var row = PixelUiBuild.Rect($"Cmd_{i}", right,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(20f, -64f - i * 32f));
                row.sizeDelta = new Vector2(240f, 28f);
                var selected = i == 0;
                if (selected)
                    PixelUiBuild.Flat(row, new Color(1f, 1f, 1f, 0.10f));
                PixelUiBuild.Label(row, "Label", (selected ? "> " : "  ") + Commands[i],
                    PixelUiArt.SizeBody, FontStyle.Normal,
                    selected ? PixelUiArt.Accent : PixelUiArt.Cream,
                    new Vector2(8f, -2f), new Vector2(220f, 24f));
            }

            PixelUiBuild.Label(right, "Hint", "[ESC] close", PixelUiArt.SizeHint, FontStyle.Normal,
                PixelUiArt.Sky, new Vector2(20f, -270f), new Vector2(240f, 22f));
        }

        void CacheRefs(RectTransform root)
        {
            var nameT = root.Find("Party/Slot_0/Name");
            if (nameT != null)
                _heroName = nameT.GetComponent<Text>();
            var hpFill = root.Find("Party/Slot_0/Hp/Fill");
            if (hpFill != null)
                _heroHpFill = hpFill.GetComponent<Image>();
            var hpLab = root.Find("Party/Slot_0/Hp/Label");
            if (hpLab != null)
                _heroHp = hpLab.GetComponent<Text>();
        }

        void RefreshFonts()
        {
            if (_root == null)
                return;

            foreach (var t in _root.GetComponentsInChildren<Text>(true))
            {
                if (t == null)
                    continue;
                var n = t.gameObject.name;
                if (n is "Title" or "Name")
                    PixelUiBuild.ApplyFont(t, n == "Title" ? PixelUiArt.SizeTitle : PixelUiArt.SizeBody,
                        FontStyle.Bold, n == "Title" ? PixelUiArt.Accent : PixelUiArt.Cream);
                else if (n == "Label")
                {
                    var selected = t.text.StartsWith("> ");
                    PixelUiBuild.ApplyFont(t, PixelUiArt.SizeBody, FontStyle.Normal,
                        selected ? PixelUiArt.Accent : PixelUiArt.Cream);
                }
                else if (n == "Hint")
                    PixelUiBuild.ApplyFont(t, PixelUiArt.SizeHint, FontStyle.Normal, PixelUiArt.Sky);
                else
                    PixelUiBuild.ApplyFont(t, PixelUiArt.SizeHint, FontStyle.Bold, PixelUiArt.Cream);
            }

            // bar fills keep their gameplay colors
            if (_heroHpFill != null)
                _heroHpFill.color = PixelUiArt.Hp;
            for (var i = 1; i < 3; i++)
            {
                var fill = _root.Find($"Party/Slot_{i}/Hp/Fill");
                if (fill != null)
                {
                    var img = fill.GetComponent<Image>();
                    if (img != null)
                        img.color = PixelUiArt.Hp;
                }
            }
        }
    }
}
