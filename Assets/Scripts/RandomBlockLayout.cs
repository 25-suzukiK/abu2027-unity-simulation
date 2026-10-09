using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rキーで既存のEB/SBを初期状態に戻し、タワー10か所にランダム配置します。
/// 空のGameObjectに追加し、Inspectorで EB_red / EB_blue / SBs とタワー範囲を設定してください。
/// </summary>
public class RandomBlockLayout : MonoBehaviour
{
    [Header("1. 既存オブジェクトの親を登録")]
    [Tooltip("Hierarchyの Block/EB_red を指定")]
    public Transform ebRedRoot;
    [Tooltip("Hierarchyの Block/EB_blue を指定")]
    public Transform ebBlueRoot;
    [Tooltip("Hierarchyの Block/SBs を指定（この直下の子GameObjectごと1つのブロックとして移動します）")]
    public Transform sbRoot;

    [Header("2. 操作")]
    public Key randomizeKey = Key.R;
    [Tooltip("Play開始時にもランダム配置する")]
    public bool randomizeOnStart = false;
    [Tooltip("0なら毎回異なる配置。0以外なら同じ配置を再現")]
    public int randomSeed = 0;

    [Header("3. タワーの配置範囲（10個）")]
    [Tooltip("各タワーの箱を置ける範囲。CenterのX/Zが範囲中央、Size X/Zが範囲の幅と奥行き、Floor Yが最下段EBの底面高さ")]
    public List<TowerArea> towerAreas = new List<TowerArea>();

    [Header("4. 箱の積み方")]
    [Tooltip("EBの1段あたりの高さ。実際のEBの高さに合わせてください")]
    [Min(0.001f)] public float ebHeight = 0.20f;
    [Tooltip("上の段の箱を、下の段の中心から最大何mずらすか")]
    [Min(0f)] public float upperLayerRandomOffset = 0.04f;

    [Header("回転の設定")]
    [Tooltip("箱のY軸回転をランダムにする")]
    public bool randomizeYaw = true;
    [Tooltip("チェックを入れると0〜360度の完全任意角度、チェックなしだと0/90/180/270度の90度刻み")]
    public bool randomizeYawArbitrary = false;

    [Header("5. タワーの色制限（EB・SB共通）")]
    [Tooltip("赤ブロック（EB/SB）しか置けないタワーの番号リスト（0始まり）")]
    public List<int> redOnlyTowerIndices = new List<int> { 0 };
    [Tooltip("青ブロック（EB/SB）しか置けないタワーの番号リスト（0始まり）")]
    public List<int> blueOnlyTowerIndices = new List<int> { 1 };

    [Header("6. タワー状態の出現ウェイト（確率重み）")]
    [Tooltip("なし（空エリア）の出現割合")]
    [Min(0)] public int weightNone = 1;
    [Tooltip("EB 1段のみ（SBなし）の出現割合")]
    [Min(0)] public int weightEb1Only = 2;
    [Tooltip("EB 2段のみ（SBなし）の出現割合")]
    [Min(0)] public int weightEb2Only = 3;
    [Tooltip("EB 2段 + SB の出現割合")]
    [Min(0)] public int weightEb2AndSb = 4;

    [Header("7. 表示")]
    public bool drawAreaGizmos = true;

    public enum TowerState
    {
        None,         // なし
        EB1_Only,     // EB 1段のみ (SBなし)
        EB2_Only,     // EB 2段のみ (SBなし)
        EB2_With_SB   // EB 2段 + SB
    }

    [Serializable]
    public class TowerArea
    {
        public string label = "Tower";
        public Vector3 center = Vector3.zero;
        [Min(0.01f)] public float sizeX = 0.35f;
        [Min(0.01f)] public float sizeZ = 0.35f;
        public float floorY = 0f;
    }

    private class BlockInfo
    {
        public Transform transform;
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public Vector3 originalLocalScale;
        public float halfHeight;
        public Vector3 halfSize;
    }

    private readonly List<BlockInfo> redEBs = new List<BlockInfo>();
    private readonly List<BlockInfo> blueEBs = new List<BlockInfo>();
    private readonly List<BlockInfo> skyBlocks = new List<BlockInfo>();
    private bool captured;

    private void Start()
    {
        CaptureOriginalState();
        if (randomizeOnStart) RandomizeLayout();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[randomizeKey].wasPressedThisFrame)
            RandomizeLayout();
    }

    [ContextMenu("Capture Original State")]
    public void CaptureOriginalState()
    {
        redEBs.Clear();
        blueEBs.Clear();
        skyBlocks.Clear();

        CollectBlocks(ebRedRoot, redEBs, topLevelOnly: false);
        CollectBlocks(ebBlueRoot, blueEBs, topLevelOnly: false);
        CollectBlocks(sbRoot, skyBlocks, topLevelOnly: true);
        captured = true;

        Debug.Log($"[RandomBlockLayout] 読み込み: 赤EB {redEBs.Count}個 / 青EB {blueEBs.Count}個 / SB {skyBlocks.Count}個", this);
    }

    private void CollectBlocks(Transform root, List<BlockInfo> result, bool topLevelOnly)
    {
        if (root == null) return;

        if (topLevelOnly)
        {
            foreach (Transform child in root)
            {
                Bounds bounds = CalculateCombinedBounds(child);
                result.Add(new BlockInfo
                {
                    transform = child,
                    originalPosition = child.position,
                    originalRotation = child.rotation,
                    originalLocalScale = child.localScale,
                    halfHeight = bounds.extents.y,
                    halfSize = bounds.extents
                });
            }
        }
        else
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                if (t.GetComponent<Renderer>() == null && t.GetComponent<Collider>() == null) continue;

                bool hasGeometryDescendant = false;
                foreach (Transform child in t.GetComponentsInChildren<Transform>(true))
                {
                    if (child == t) continue;
                    if (child.GetComponent<Renderer>() != null || child.GetComponent<Collider>() != null)
                    {
                        hasGeometryDescendant = true;
                        break;
                    }
                }
                if (hasGeometryDescendant) continue;

                Bounds bounds;
                Renderer renderer = t.GetComponent<Renderer>();
                Collider collider = t.GetComponent<Collider>();
                if (renderer != null) bounds = renderer.bounds;
                else if (collider != null) bounds = collider.bounds;
                else continue;

                result.Add(new BlockInfo
                {
                    transform = t,
                    originalPosition = t.position,
                    originalRotation = t.rotation,
                    originalLocalScale = t.localScale,
                    halfHeight = bounds.extents.y,
                    halfSize = bounds.extents
                });
            }
        }
    }

    private Bounds CalculateCombinedBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            return b;
        }

        return new Bounds(target.position, Vector3.one * 0.1f);
    }

    [ContextMenu("Randomize Tower Layout")]
    public void RandomizeLayout()
    {
        if (!captured) CaptureOriginalState();
        if (towerAreas == null || towerAreas.Count == 0)
        {
            Debug.LogWarning("[RandomBlockLayout] Inspectorの Tower Areas にタワー範囲を追加してください。", this);
            return;
        }

        RestoreAll();

        System.Random rng = randomSeed == 0
            ? new System.Random(Guid.NewGuid().GetHashCode())
            : new System.Random(randomSeed);

        List<BlockInfo> redPool = new List<BlockInfo>(redEBs);
        List<BlockInfo> bluePool = new List<BlockInfo>(blueEBs);
        List<BlockInfo> sbPool = new List<BlockInfo>(skyBlocks);

        Shuffle(redPool, rng);
        Shuffle(bluePool, rng);
        Shuffle(sbPool, rng);

        int placedTowers = 0;
        for (int i = 0; i < towerAreas.Count; i++)
        {
            TowerArea area = towerAreas[i];
            if (area == null) continue;

            TowerState state = SelectRandomState(rng);
            if (state == TowerState.None) continue;

            int targetEbLayers = (state == TowerState.EB1_Only) ? 1 : 2;
            bool placeSB = (state == TowerState.EB2_With_SB);

            bool isRedOnlyTower = redOnlyTowerIndices != null && redOnlyTowerIndices.Contains(i);
            bool isBlueOnlyTower = blueOnlyTowerIndices != null && blueOnlyTowerIndices.Contains(i);

            List<BlockInfo> towerEBs = new List<BlockInfo>();
            for (int layer = 0; layer < targetEbLayers; layer++)
            {
                BlockInfo eb = TakeEB(redPool, bluePool, i, rng);
                if (eb == null) break;
                towerEBs.Add(eb);
            }

            Vector3 previousPosition = Vector3.zero;

            // --- EBの配置 ---
            for (int layer = 0; layer < towerEBs.Count; layer++)
            {
                BlockInfo eb = towerEBs[layer];
                Quaternion rot = DetermineRotation(isRedOnlyTower, isBlueOnlyTower, isSB: false, rng);
                Vector2 rotatedHalfSize = GetRotatedHalfSize(eb.halfSize, rot);

                Vector3 pos;
                if (layer == 0)
                {
                    pos = RandomPoint(area, rotatedHalfSize, rng);
                    pos.y = area.floorY + eb.halfHeight;
                }
                else
                {
                    pos = previousPosition;
                    pos.x += NextRange(rng, -upperLayerRandomOffset, upperLayerRandomOffset);
                    pos.z += NextRange(rng, -upperLayerRandomOffset, upperLayerRandomOffset);
                    pos.y = area.floorY + layer * ebHeight + eb.halfHeight;
                    pos = ClampToArea(pos, area, rotatedHalfSize);
                }

                MoveBlock(eb, pos, rot);
                previousPosition = pos;
            }

            // --- SBの配置 ---
            if (placeSB && towerEBs.Count == 2 && sbPool.Count > 0)
            {
                BlockInfo sb = sbPool[0];
                sbPool.RemoveAt(0);

                Quaternion sbRot = DetermineRotation(isRedOnlyTower, isBlueOnlyTower, isSB: true, rng);
                Vector2 rotatedHalfSize = GetRotatedHalfSize(sb.halfSize, sbRot);

                Vector3 sbPos = previousPosition;
                sbPos.x += NextRange(rng, -upperLayerRandomOffset, upperLayerRandomOffset);
                sbPos.z += NextRange(rng, -upperLayerRandomOffset, upperLayerRandomOffset);
                sbPos.y = area.floorY + (2 * ebHeight) + sb.halfHeight;

                sbPos = ClampToArea(sbPos, area, rotatedHalfSize);

                MoveBlock(sb, sbPos, sbRot);
            }

            placedTowers++;
        }

        Debug.Log($"[RandomBlockLayout] 完了: {placedTowers}か所のタワーを配置完了。", this);
    }

    private TowerState SelectRandomState(System.Random rng)
    {
        int totalWeight = weightNone + weightEb1Only + weightEb2Only + weightEb2AndSb;
        if (totalWeight <= 0) return TowerState.EB2_With_SB;

        int roll = rng.Next(totalWeight);
        if (roll < weightNone) return TowerState.None;
        roll -= weightNone;

        if (roll < weightEb1Only) return TowerState.EB1_Only;
        roll -= weightEb1Only;

        if (roll < weightEb2Only) return TowerState.EB2_Only;

        return TowerState.EB2_With_SB;
    }

    /// <summary>
    /// 回転の決定
    /// ・赤だけタワー: 反転なし（赤が上）
    /// ・青だけタワー: 180度反転（青が上）
    /// ・それ以外のタワー: 50%の確率で反転（赤上または青上）
    /// </summary>
    private Quaternion DetermineRotation(bool isRedOnlyTower, bool isBlueOnlyTower, bool isSB, System.Random rng)
    {
        float yawAngle = 0f;
        if (randomizeYaw)
        {
            yawAngle = randomizeYawArbitrary
                ? NextRange(rng, 0f, 360f)
                : rng.Next(4) * 90f;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, yawAngle, 0f);

        bool shouldFlip = false;

        if (isBlueOnlyTower)
        {
            shouldFlip = true; // 青限定タワーなら必ず青が上（反転）
        }
        else if (isRedOnlyTower)
        {
            shouldFlip = false; // 赤限定タワーなら必ず赤が上（反転なし）
        }
        else
        {
            // 限定なしのタワー：50%の確率で赤上か青上かをランダム決定
            shouldFlip = (rng.Next(2) == 0);
        }

        if (shouldFlip)
        {
            Quaternion flipRotation = Quaternion.Euler(180f, 0f, 0f);
            return yawRotation * flipRotation;
        }

        return yawRotation;
    }

    private Vector2 GetRotatedHalfSize(Vector3 originalHalfSize, Quaternion rotation)
    {
        Vector3 h = originalHalfSize;
        Vector3[] corners = new Vector3[]
        {
            rotation * new Vector3( h.x, 0,  h.z),
            rotation * new Vector3( h.x, 0, -h.z),
            rotation * new Vector3(-h.x, 0, -h.z),
            rotation * new Vector3(-h.x, 0, -h.z)
        };

        float maxX = 0f;
        float maxZ = 0f;
        foreach (var c in corners)
        {
            maxX = Mathf.Max(maxX, Mathf.Abs(c.x));
            maxZ = Mathf.Max(maxZ, Mathf.Abs(c.z));
        }

        return new Vector2(maxX, maxZ);
    }

    private Vector3 RandomPoint(TowerArea area, Vector2 rotatedHalfSize, System.Random rng)
    {
        float safeExtentX = Mathf.Max(0f, area.sizeX * 0.5f - rotatedHalfSize.x);
        float safeExtentZ = Mathf.Max(0f, area.sizeZ * 0.5f - rotatedHalfSize.y);

        float x = area.center.x + NextRange(rng, -safeExtentX, safeExtentX);
        float z = area.center.z + NextRange(rng, -safeExtentZ, safeExtentZ);
        return new Vector3(x, area.floorY, z);
    }

    private Vector3 ClampToArea(Vector3 pos, TowerArea area, Vector2 rotatedHalfSize)
    {
        float minX = area.center.x - area.sizeX * 0.5f + rotatedHalfSize.x;
        float maxX = area.center.x + area.sizeX * 0.5f - rotatedHalfSize.x;
        float minZ = area.center.z - area.sizeZ * 0.5f + rotatedHalfSize.y;
        float maxZ = area.center.z + area.sizeZ * 0.5f - rotatedHalfSize.y;

        pos.x = Mathf.Clamp(pos.x, Mathf.Min(minX, area.center.x), Mathf.Max(maxX, area.center.x));
        pos.z = Mathf.Clamp(pos.z, Mathf.Min(minZ, area.center.z), Mathf.Max(maxZ, area.center.z));
        return pos;
    }

    private void MoveBlock(BlockInfo block, Vector3 position, Quaternion rotation)
    {
        block.transform.position = position;
        block.transform.rotation = rotation;
    }

    private BlockInfo TakeEB(List<BlockInfo> redPool, List<BlockInfo> bluePool, int towerIndex, System.Random rng)
    {
        bool redOnly = redOnlyTowerIndices != null && redOnlyTowerIndices.Contains(towerIndex);
        bool blueOnly = blueOnlyTowerIndices != null && blueOnlyTowerIndices.Contains(towerIndex);

        if (redOnly) return TakeRandom(redPool, rng);
        if (blueOnly) return TakeRandom(bluePool, rng);

        if (redPool.Count == 0) return TakeRandom(bluePool, rng);
        if (bluePool.Count == 0) return TakeRandom(redPool, rng);
        return rng.Next(2) == 0 ? TakeRandom(redPool, rng) : TakeRandom(bluePool, rng);
    }

    private BlockInfo TakeRandom(List<BlockInfo> pool, System.Random rng)
    {
        if (pool.Count == 0) return null;
        int index = rng.Next(pool.Count);
        BlockInfo result = pool[index];
        pool.RemoveAt(index);
        return result;
    }

    private void RestoreAll()
    {
        foreach (BlockInfo b in redEBs) Restore(b);
        foreach (BlockInfo b in blueEBs) Restore(b);
        foreach (BlockInfo b in skyBlocks) Restore(b);
    }

    private void Restore(BlockInfo b)
    {
        if (b == null || b.transform == null) return;
        b.transform.position = b.originalPosition;
        b.transform.rotation = b.originalRotation;
        b.transform.localScale = b.originalLocalScale;
    }

    private float NextRange(System.Random rng, float min, float max)
    {
        return (float)(min + rng.NextDouble() * (max - min));
    }

    private void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAreaGizmos || towerAreas == null) return;
        for (int i = 0; i < towerAreas.Count; i++)
        {
            TowerArea area = towerAreas[i];
            if (area == null) continue;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(
                new Vector3(area.center.x, area.floorY, area.center.z),
                new Vector3(area.sizeX, 0.02f, area.sizeZ));
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                new Vector3(area.center.x, area.floorY + 0.05f, area.center.z),
                string.IsNullOrEmpty(area.label) ? $"Tower {i + 1}" : area.label);
#endif
        }
    }
}