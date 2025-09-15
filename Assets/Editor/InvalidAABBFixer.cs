using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class InvalidAABBFixer : EditorWindow
{
    private Vector2 scrollPos;
    private string lastReport = "";

    [MenuItem("Tools/KAFKA Fix - Invalid AABB/NaN Cleanup")]
    public static void ShowWindow()
    {
        var win = GetWindow<InvalidAABBFixer>("AABB/NaN Cleanup");
        win.minSize = new Vector2(420, 320);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "シーン内の NaN/Infinity 変換, 極端な位置/スケール, 無効なBounds/Collider を検出し自動修正します。\n事前にシーンを保存してください。",
            MessageType.Warning);

        if (GUILayout.Button("🔧 Scan & Fix Now"))
        {
            RunScanAndFix();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Report", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    [MenuItem("Tools/KAFKA Fix/Run Invalid AABB/NaN Cleanup (Quick)")]
    public static void RunQuick()
    {
        RunScanAndFix();
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);
    private static bool IsFinite(Quaternion q) => IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);

    private static float ClampAbs(float v, float maxAbs)
    {
        if (!IsFinite(v)) return 0f;
        return Mathf.Clamp(v, -maxAbs, maxAbs);
    }

    private static Vector3 ClampAbs(Vector3 v, float maxAbs)
    {
        return new Vector3(ClampAbs(v.x, maxAbs), ClampAbs(v.y, maxAbs), ClampAbs(v.z, maxAbs));
    }

    private static string FixTransform(Transform t)
    {
        var lines = new List<string>();

        // Fix non-finite locals
        if (!IsFinite(t.localPosition))
        {
            t.localPosition = Vector3.zero;
            lines.Add($"{GetPath(t)}: localPosition=NaN/Inf → zero");
        }
        if (!IsFinite(t.localRotation))
        {
            t.localRotation = Quaternion.identity;
            lines.Add($"{GetPath(t)}: localRotation=NaN/Inf → identity");
        }
        if (!IsFinite(t.localScale))
        {
            t.localScale = Vector3.one;
            lines.Add($"{GetPath(t)}: localScale=NaN/Inf → one");
        }

        // Clamp extreme transforms
        const float posLimit = 100000f; // Unity warning threshold vicinity
        var worldPos = t.position;
        if (!IsFinite(worldPos) || Mathf.Abs(worldPos.x) > posLimit || Mathf.Abs(worldPos.y) > posLimit || Mathf.Abs(worldPos.z) > posLimit)
        {
            var clamped = ClampAbs(worldPos, 10000f);
            t.position = clamped;
            lines.Add($"{GetPath(t)}: position out-of-range → clamped to {clamped}");
        }

        var lossy = t.lossyScale;
        if (!IsFinite(lossy) || Mathf.Abs(lossy.x) > 10000f || Mathf.Abs(lossy.y) > 10000f || Mathf.Abs(lossy.z) > 10000f)
        {
            t.localScale = Vector3.one;
            lines.Add($"{GetPath(t)}: lossyScale out-of-range → localScale=one");
        }

        // Warn for negative scale with BoxCollider
        var hasNegative = (lossy.x < 0f) || (lossy.y < 0f) || (lossy.z < 0f);
        if (hasNegative)
        {
            var box = t.GetComponent<BoxCollider>();
            if (box != null)
            {
                lines.Add($"{GetPath(t)}: negative scale with BoxCollider → consider MeshCollider or remove negative scale");
            }
        }

        return string.Join("\n", lines);
    }

    private static string FixRendererBounds(Renderer r)
    {
        var lines = new List<string>();
        bool invalid = false;
        try
        {
            var b = r.bounds; // accessing may throw warnings but we can still check extents
            if (!IsFinite(b.center.x) || !IsFinite(b.center.y) || !IsFinite(b.center.z) ||
                !IsFinite(b.extents.x) || !IsFinite(b.extents.y) || !IsFinite(b.extents.z))
            {
                invalid = true;
            }
            else if (b.extents.magnitude > 1e6f)
            {
                invalid = true;
            }
        }
        catch
        {
            invalid = true;
        }

        if (!invalid) return "";

        if (r is SkinnedMeshRenderer smr)
        {
            if (smr.sharedMesh != null)
            {
                var m = smr.sharedMesh;
                var mb = m.bounds;
                if (!IsFinite(mb.center.x) || !IsFinite(mb.extents.x))
                {
                    m.RecalculateBounds();
                    mb = m.bounds;
                }
                if (IsFinite(mb.center.x) && IsFinite(mb.extents.x))
                {
                    smr.localBounds = mb;
                    lines.Add($"{GetPath(r.transform)}: SkinnedMesh localBounds ← mesh.bounds");
                }
                else
                {
                    smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                    lines.Add($"{GetPath(r.transform)}: SkinnedMesh localBounds reset to unit bounds");
                }
            }
            else
            {
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                lines.Add($"{GetPath(r.transform)}: SkinnedMesh localBounds reset (no mesh)");
            }
        }
        else if (r is MeshRenderer mr)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                mf.sharedMesh.RecalculateBounds();
                lines.Add($"{GetPath(r.transform)}: Mesh bounds recalculated");
            }
        }

        return string.Join("\n", lines);
    }

    private static string FixCollider(Collider c)
    {
        var lines = new List<string>();
        if (c is BoxCollider bc)
        {
            if (!IsFinite(bc.center) || !IsFinite(bc.size))
            {
                bc.center = Vector3.zero;
                bc.size = Vector3.one;
                lines.Add($"{GetPath(c.transform)}: BoxCollider center/size reset");
            }
        }
        else if (c is SphereCollider sc)
        {
            if (!IsFinite(sc.center) || !IsFinite(sc.radius) || sc.radius <= 0f || sc.radius > 10000f)
            {
                sc.center = Vector3.zero;
                sc.radius = 0.5f;
                lines.Add($"{GetPath(c.transform)}: SphereCollider center/radius reset");
            }
        }
        else if (c is CapsuleCollider cc)
        {
            if (!IsFinite(cc.center) || !IsFinite(cc.radius) || !IsFinite(cc.height) || cc.radius <= 0f || cc.height <= 0f)
            {
                cc.center = Vector3.zero;
                cc.radius = 0.25f;
                cc.height = 1f;
                lines.Add($"{GetPath(c.transform)}: CapsuleCollider reset");
            }
        }
        return string.Join("\n", lines);
    }

    private static string GetPath(Transform t)
    {
        var stack = new List<string>();
        while (t != null)
        {
            stack.Add(t.name);
            t = t.parent;
        }
        stack.Reverse();
        return string.Join("/", stack);
    }

    private static void RunScanAndFix()
    {
        var reportLines = new List<string>();
        int fixedTransforms = 0, fixedRenderers = 0, fixedColliders = 0;

        var transforms = Object.FindObjectsOfType<Transform>(true);
        foreach (var t in transforms)
        {
            var r = FixTransform(t);
            if (!string.IsNullOrEmpty(r))
            {
                reportLines.Add(r);
                fixedTransforms++;
                EditorUtility.SetDirty(t);
            }
        }

        var renderers = Object.FindObjectsOfType<Renderer>(true);
        foreach (var r in renderers)
        {
            var s = FixRendererBounds(r);
            if (!string.IsNullOrEmpty(s))
            {
                reportLines.Add(s);
                fixedRenderers++;
                EditorUtility.SetDirty(r);
            }
        }

        var colliders = Object.FindObjectsOfType<Collider>(true);
        foreach (var c in colliders)
        {
            var s = FixCollider(c);
            if (!string.IsNullOrEmpty(s))
            {
                reportLines.Add(s);
                fixedColliders++;
                EditorUtility.SetDirty(c);
            }
        }

        if (reportLines.Count == 0)
        {
            var msg = "問題は検出されませんでした。";
            Debug.Log($"✅ AABB/NaN Cleanup: {msg}");
            var win = GetWindow<InvalidAABBFixer>(false, null, false);
            if (win != null) win.lastReport = msg;
            return;
        }

        var header = $"Transforms: {fixedTransforms}, Renderers: {fixedRenderers}, Colliders: {fixedColliders}";
        Debug.Log($"✅ AABB/NaN Cleanup 完了 → {header}");
        var full = header + "\n\n" + string.Join("\n", reportLines);
        var w = GetWindow<InvalidAABBFixer>(false, null, false);
        if (w != null)
        {
            w.lastReport = full;
            w.Repaint();
        }
    }
}

