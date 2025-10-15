/*
 * SpriteFracturer2D.cs
 * 
 * Author: Parein Jean-Philippe
 * Version: 1.0
 * Description:
 *  Runtime 2D sprite fracturing system for Unity. 
 *  Splits a sprite into multiple pieces with physics-based explosion,
 *  optional blinking effect, timed destruction, and editor utilities.
 *
 * Compatibility:
 *   - Works with all Unity render pipelines:
 *       Built-in Render Pipeline
 *       Universal Render Pipeline (URP)
 *       High Definition Render Pipeline (HDRP, with 2D Renderer)
 *   - Requires the sprite's texture to be Read/Write Enabled in import settings.
 * 
 * Tested with:
 *   - Unity 2022.3+
 *   - Unity 6 (URP 2D)
 * License: MIT 
 */

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SpriteFracture
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(PolygonCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class SpriteFracturer2D : MonoBehaviour
    {
        public enum TriggerMode { AutoStart, Collision, Trigger }

        [Header("Trigger Mode")]
        [Tooltip("How the fracture is triggered: automatically, on collision, or via trigger.")]
        public TriggerMode triggerMode = TriggerMode.AutoStart;

        [Header("Fracture Settings")]
        [Tooltip("Number of columns used to split the sprite.")]
        public int columns = 4;
        [Tooltip("Number of rows used to split the sprite.")]
        public int rows = 4;

        [Header("Physics Settings")]
        [Tooltip("Mass of each fractured piece.")]
        public float pieceMass = 0.2f;
        [Tooltip("Force applied to pieces during explosion.")]
        public float explosionForce = 300f;
        [Tooltip("Adds upward lift to the explosion direction.")]
        public float upwardModifier = 0.5f;

        [Header("Lifetime / Destruction")]
        [Tooltip("Lifetime of each fractured piece before destruction.")]
        public float pieceLifetime = 5f;
        [Tooltip("If true, fractured pieces will be destroyed automatically after lifetime.")]
        public bool destroyPieces = true;
        [Tooltip("If true, pieces get destroyed when they collide with other objects (not with each other).")]
        public bool destroyOnCollision = false;
        [Tooltip("Delay after spawn before collision-based destruction arms (prevents self-collision on spawn).")]
        public float collisionArmDelay = 0.05f;

        [Header("Blink Effect")]
        [Tooltip("Enable blinking effect before destruction.")]
        public bool useBlink = true;
        [Tooltip("Duration of blinking before the piece is destroyed.")]
        public float blinkDuration = 1f;
        [Tooltip("Blink frequency (times per second).")]
        public float blinkFrequency = 10f;

        [Header("Collider Options")]
        [Tooltip("If true, fractured piece colliders are set as triggers so they can pass through objects.")]
        public bool piecesAsTrigger = false;

        [Header("Events")]
        [Tooltip("Called when the object fractures.")]
        public UnityEvent onFracture;
        [Tooltip("Called when a fractured piece is destroyed.")]
        public UnityEvent onPieceDestroyed;

        [Header("Misc")]
        [Tooltip("Delay before the automatic fracture starts (only used in AutoStart mode).")]
        public float delayBeforeFracture = 1f;
        [Tooltip("Show fracture grid in Scene View when selected.")]
        public bool showGridGizmos = true;

        private SpriteRenderer sr;
        private Texture2D sourceTex;
        private bool fractured = false;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.bodyType = triggerMode == TriggerMode.Collision
                ? RigidbodyType2D.Dynamic
                : RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;

            PolygonCollider2D col = GetComponent<PolygonCollider2D>();
            col.isTrigger = triggerMode == TriggerMode.Trigger;

            if (triggerMode == TriggerMode.AutoStart)
                StartCoroutine(FractureAfterDelay());
        }

        private IEnumerator FractureAfterDelay()
        {
            yield return new WaitForSeconds(delayBeforeFracture);
            StartCoroutine(Fracture());
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (triggerMode != TriggerMode.Collision || fractured) return;
            fractured = true;
            StartCoroutine(Fracture());
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggerMode != TriggerMode.Trigger || fractured) return;
            fractured = true;
            StartCoroutine(Fracture());
        }

        /// <summary>
        /// Main fracture process: splits the sprite into pieces, applies physics and handles cleanup.
        /// </summary>
        public IEnumerator Fracture()
        {
            if (sr == null || sr.sprite == null)
                yield break;

            sourceTex = sr.sprite.texture;
            if (!sourceTex.isReadable)
            {
                Debug.LogError("Texture not readable: enable 'Read/Write Enabled' in import settings.");
                yield break;
            }

            fractured = true;
            onFracture?.Invoke();

            Collider2D mainCol = GetComponent<Collider2D>();
            if (mainCol) mainCol.enabled = false;

            Sprite sprite = sr.sprite;
            Rect texRect = sprite.textureRect;
            float ppu = sprite.pixelsPerUnit;
            sr.enabled = false;

            int pw = Mathf.CeilToInt(texRect.width / columns);
            int ph = Mathf.CeilToInt(texRect.height / rows);
            Vector3 origin = transform.position;

            GameObject parent = new GameObject("Fracture_" + gameObject.name);
            parent.transform.position = transform.position;

            var tempTextures = new System.Collections.Generic.List<Texture2D>();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int px = (int)texRect.x + x * pw;
                    int py = (int)texRect.y + y * ph;
                    int w = Mathf.Min(pw, (int)texRect.xMax - px);
                    int h = Mathf.Min(ph, (int)texRect.yMax - py);
                    if (w <= 0 || h <= 0) continue;

                    Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    Color[] pixels = sourceTex.GetPixels(px, py, w, h);
                    tex.SetPixels(pixels);
                    tex.Apply();

                    bool hasVisiblePixel = false;
                    foreach (Color c in pixels)
                    {
                        if (c.a > 0.01f) { hasVisiblePixel = true; break; }
                    }
                    if (!hasVisiblePixel) { Destroy(tex); continue; }

                    tempTextures.Add(tex);

                    Sprite pieceSprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                        new Vector2(0.5f, 0.5f), ppu);

                    GameObject piece = new GameObject($"Piece_{x}_{y}");
                    piece.transform.parent = parent.transform;
                    piece.transform.position = PiecePos(sprite, texRect, x, y, w, h, ppu);
                    piece.transform.rotation = transform.rotation;
                    piece.transform.localScale = transform.lossyScale;

                    SpriteRenderer psr = piece.AddComponent<SpriteRenderer>();
                    psr.sprite = pieceSprite;
                    psr.sortingLayerID = sr.sortingLayerID;
                    psr.sortingOrder = sr.sortingOrder;

                    Rigidbody2D rb = piece.AddComponent<Rigidbody2D>();
                    rb.mass = pieceMass;
                    rb.gravityScale = 1f;
                    rb.linearDamping = 0.5f;
                    rb.angularDamping = 0.5f;

                    PolygonCollider2D poly = piece.AddComponent<PolygonCollider2D>();
                    poly.isTrigger = piecesAsTrigger;

                    // Destruction management
                    if (destroyOnCollision)
                    {
                        PieceCollisionHandler handler = piece.AddComponent<PieceCollisionHandler>();
                        handler.Init(psr, parent.transform, useBlink, pieceLifetime, blinkDuration, blinkFrequency, collisionArmDelay, piecesAsTrigger, onPieceDestroyed);
                    }
                    else if (destroyPieces)
                    {
                        if (useBlink)
                            piece.AddComponent<BlinkBeforeDestroy>().Init(psr, pieceLifetime, blinkDuration, blinkFrequency, onPieceDestroyed);
                        else
                            StartCoroutine(DestroyAfterDelay(piece, pieceLifetime));
                    }

                    // Explosion physics
                    Vector2 dir = ((Vector2)piece.transform.position - (Vector2)origin).normalized;
                    dir = (dir + Random.insideUnitCircle * 0.3f).normalized;
                    rb.AddForce((dir + Vector2.up * upwardModifier).normalized * explosionForce);
                    rb.AddTorque(Random.Range(-100f, 100f));
                }
                yield return null;
            }

            Destroy(gameObject, 0.1f);
            if (destroyPieces && !destroyOnCollision)
                Destroy(parent, pieceLifetime + 0.2f);

            yield return new WaitForSeconds(pieceLifetime + 0.5f);
            foreach (var t in tempTextures)
                if (t != null) Destroy(t);
        }

        private IEnumerator DestroyAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            onPieceDestroyed?.Invoke();
            Destroy(obj);
        }

        private Vector3 PiecePos(Sprite sprite, Rect texRect, int col, int row, int w, int h, float ppu)
        {
            Vector2 pivot = sprite.pivot / ppu;
            float pieceW = w / ppu, pieceH = h / ppu;
            float xPos = (col * (texRect.width / columns)) / ppu + pieceW / 2f - pivot.x + transform.position.x;
            float yPos = (row * (texRect.height / rows)) / ppu + pieceH / 2f - pivot.y + transform.position.y;
            return new Vector3(xPos, yPos, transform.position.z);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGridGizmos) return;
            if (!GetComponent<SpriteRenderer>()?.sprite) return;

            Gizmos.color = Color.yellow;
            var sr = GetComponent<SpriteRenderer>();
            var bounds = sr.bounds;

            for (int x = 1; x < columns; x++)
                Gizmos.DrawLine(bounds.min + new Vector3(bounds.size.x * x / columns, 0),
                                bounds.min + new Vector3(bounds.size.x * x / columns, bounds.size.y));
            for (int y = 1; y < rows; y++)
                Gizmos.DrawLine(bounds.min + new Vector3(0, bounds.size.y * y / rows),
                                bounds.min + new Vector3(bounds.size.x, bounds.size.y * y / rows));

            UnityEditor.Handles.Label(bounds.center + Vector3.up * 0.2f, "Fracture Grid Preview");
        }
#endif
    }

    // Handles destruction logic for individual pieces
    public class PieceCollisionHandler : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Transform root;
        private bool useBlink;
        private float lifetime, blinkDuration, blinkFreq, armDelay;
        private bool armed = false;
        private bool asTrigger;
        private UnityEvent onPieceDestroyed;

        public void Init(SpriteRenderer r, Transform parentRoot, bool blink, float life, float bDur, float bFreq, float armDelaySec, bool piecesAreTrigger, UnityEvent onDestroyed)
        {
            sr = r;
            root = parentRoot;
            useBlink = blink;
            lifetime = life;
            blinkDuration = bDur;
            blinkFreq = bFreq;
            armDelay = Mathf.Max(0f, armDelaySec);
            asTrigger = piecesAreTrigger;
            onPieceDestroyed = onDestroyed;
            StartCoroutine(ArmAfterDelay());
            if (lifetime > 0f) Destroy(gameObject, lifetime);
        }

        private IEnumerator ArmAfterDelay() { yield return new WaitForSeconds(armDelay); armed = true; }

        private bool IsSameFracture(Transform other) => other != null && other.parent == root;

        private void TryDestroyNow()
        {
            if (!armed) return;
            onPieceDestroyed?.Invoke();

            if (useBlink)
            {
                var blink = gameObject.AddComponent<BlinkBeforeDestroy>();
                blink.TriggerNow(blinkDuration, blinkFreq, onPieceDestroyed);
            }
            else Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (IsSameFracture(col.transform)) return;
            TryDestroyNow();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!asTrigger) return;
            if (IsSameFracture(other.transform)) return;
            TryDestroyNow();
        }
    }

    // Handles blinking effect before destruction
    public class BlinkBeforeDestroy : MonoBehaviour
    {
        private SpriteRenderer sr;
        private UnityEvent onDestroyed;

        public void Init(SpriteRenderer target, float lifetime, float blinkDuration, float frequency, UnityEvent callback)
        {
            sr = target;
            onDestroyed = callback;
            StartCoroutine(BlinkTimed(lifetime, blinkDuration, frequency));
            Destroy(gameObject, lifetime);
        }

        public void TriggerNow(float blinkDuration, float frequency, UnityEvent callback)
        {
            sr = GetComponent<SpriteRenderer>();
            onDestroyed = callback;
            StartCoroutine(BlinkNow(blinkDuration, frequency));
        }

        private IEnumerator BlinkTimed(float lifetime, float blinkDuration, float frequency)
        {
            yield return new WaitForSeconds(Mathf.Max(0, lifetime - blinkDuration));
            yield return BlinkNow(blinkDuration, frequency);
        }

        private IEnumerator BlinkNow(float duration, float frequency)
        {
            float elapsed = 0f;
            float interval = 1f / Mathf.Max(1f, frequency);
            bool visible = true;
            while (elapsed < duration)
            {
                if (!sr) yield break;
                visible = !visible;
                sr.enabled = visible;
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
            onDestroyed?.Invoke();
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SpriteFracturer2D))]
    public class SpriteFracturer2DEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var fracturer = (SpriteFracturer2D)target;

            EditorGUILayout.Space(8);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Test Fracture Now"))
                fracturer.StartCoroutine(fracturer.Fracture());
            GUI.enabled = true;

            if (fracturer.GetComponent<SpriteRenderer>()?.sprite != null)
            {
                Texture2D tex = fracturer.GetComponent<SpriteRenderer>().sprite.texture;
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path);
                if (importer != null && !importer.isReadable)
                {
                    EditorGUILayout.HelpBox("This sprite texture is not Read/Write enabled.\nClick below to fix automatically.", MessageType.Warning);
                    if (GUILayout.Button("Enable Read/Write on Sprite"))
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                        Debug.Log($"'{tex.name}' is now Read/Write enabled.");
                    }
                }
            }
        }
    }
#endif
}