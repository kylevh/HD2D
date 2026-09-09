using UnityEngine;

namespace KVH.Game.Interaction
{
    // Marker + silhouette source. Scanner outlines every renderer under this root as one blob.
    // Highlight is X-ray (full ring even if player stands in front) — see HighlightOutlineFeature.
    // Actions live beside this: DialogueSpeaker / ItemPickup / InspectText.
    [DisallowMultipleComponent]
    public sealed class Interactable : MonoBehaviour
    {
        [SerializeField] InteractableKind kind = InteractableKind.Prop;
        [SerializeField] float range = 1.7f;
        [SerializeField] Color outlineColor = new Color(0.97f, 0.90f, 0.42f, 1f);
        [Range(0f, 1f)] [SerializeField] float fill;
        [Tooltip("Props: gold outline. NPCs: usually off — Talk prompt carries the cue.")]
        [SerializeField] bool useOutline = true;
        [Tooltip("Added on top of renderer bounds (above the prop / head).")]
        [SerializeField] Vector3 promptOffset = new Vector3(0f, 0.35f, 0f);
        [SerializeField] string promptLabel = "F";
        [SerializeField] Renderer[] renderers;

        public InteractableKind Kind => kind;
        public float Range => range;
        public Color OutlineColor => outlineColor;
        public float Fill => fill;
        public bool UseOutline => useOutline;
        public string PromptLabel
        {
            get
            {
                if (string.IsNullOrEmpty(promptLabel) || promptLabel == "E")
                    return kind == InteractableKind.Npc ? "Talk" : "F";
                return promptLabel;
            }
        }

        public Renderer[] Renderers => renderers;

        public Vector3 PromptWorldPosition
        {
            get
            {
                // Collider bounds track the interact volume; renderer bounds can drift if a
                // child mesh was left with a bad local pose (Npc Body had this).
                var col = GetComponent<Collider>();
                if (col != null)
                {
                    var b = col.bounds;
                    return new Vector3(b.center.x, b.max.y, b.center.z) + promptOffset;
                }

                if (renderers != null && renderers.Length > 0 && renderers[0] != null)
                {
                    var bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                            bounds.Encapsulate(renderers[i].bounds);
                    }

                    return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + promptOffset;
                }

                return transform.position + Vector3.up * 2f + promptOffset;
            }
        }

        void Reset()
        {
            ApplyKindDefaults();
            CollectRenderers();
        }

        void OnValidate()
        {
            range = Mathf.Max(0.1f, range);
            if (renderers == null || renderers.Length == 0)
                CollectRenderers();
        }

        void OnEnable()
        {
            if (renderers == null || renderers.Length == 0)
                CollectRenderers();
        }

        // Call from builders / context menu when changing kind in the Inspector.
        public void ApplyKindDefaults()
        {
            if (kind == InteractableKind.Npc)
            {
                // Soft sky rim + Talk chip — readable at low res without gold “loot” vibes.
                useOutline = true;
                fill = 0f;
                promptLabel = "Talk";
                promptOffset = new Vector3(0f, 0.2f, 0f);
                outlineColor = new Color(0.78f, 0.90f, 1f, 1f);
                range = Mathf.Max(range, 2.5f);
            }
            else
            {
                useOutline = true;
                promptLabel = "F";
                promptOffset = new Vector3(0f, 0.35f, 0f);
                outlineColor = new Color(0.97f, 0.90f, 0.42f, 1f);
            }
        }

        public float DistanceTo(Vector3 origin)
        {
            var col = GetComponent<Collider>();
            if (col != null)
                return Vector3.Distance(origin, col.ClosestPoint(origin));

            var cols = GetComponentsInChildren<Collider>();
            if (cols.Length == 0)
                return Vector3.Distance(origin, transform.position);

            var best = float.MaxValue;
            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null || !cols[i].enabled)
                    continue;
                var d = Vector3.Distance(origin, cols[i].ClosestPoint(origin));
                if (d < best)
                    best = d;
            }

            return best;
        }

        public void CollectRenderers()
        {
            var found = GetComponentsInChildren<Renderer>(true);
            var count = 0;
            for (var i = 0; i < found.Length; i++)
            {
                if (IsOutlineRenderer(found[i]))
                    count++;
            }

            renderers = new Renderer[count];
            var w = 0;
            for (var i = 0; i < found.Length; i++)
            {
                if (!IsOutlineRenderer(found[i]))
                    continue;
                renderers[w++] = found[i];
            }
        }

        static bool IsOutlineRenderer(Renderer r)
        {
            return r != null;
        }
    }
}
