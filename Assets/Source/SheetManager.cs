using System;
using System.Collections.Generic;
using UnityEngine;

public enum SliceAxis { Row, Column }

public class SheetManager : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("surfaceGenerator")]
    [UnityEngine.Serialization.FormerlySerializedAs("regionGenerator")]
    public SheetGenerator sheetGenerator;
    public DataSource dataSource;

    public GameObject piecePrefab;

    public event Action OnSheetsChanged;
    public event Action<SliceAxis, int> OnSliced;
    public event Action OnSlicesReset;
    public event Action OnEditStateChanged;
    public event Action OnEditsDroppedByReload;
    public event Action<Sheet, Vector3, Quaternion> OnSheetMoveCommitted;
    public event Action<int, int, int, int, SliceAxis, int> OnSliceUndone;

    public struct SliceRecord
    {
        public int pRowMin, pRowMax, pColMin, pColMax;
        public Vector3 pOffset;
        public bool pMoved;
        public Vector3 pMovedPos;
        public Quaternion pMovedRot;
        public int aRowMin, aRowMax, aColMin, aColMax;
        public int bRowMin, bRowMax, bColMin, bColMax;
        public SliceAxis axis;
        public int boundary;
    }

    public class Sheet
    {
        public int rowMin, rowMax, colMin, colMax;
        public Vector3 offset;
        public SheetPiece piece;

        public bool moved;
        public Vector3 movedLocalPos;
        public Quaternion movedLocalRot;

        public bool Contains(int visRow, int visCol) =>
            visRow >= rowMin && visRow <= rowMax && visCol >= colMin && visCol <= colMax;
    }

    private struct ColorOverride
    {
        public int rMin, rMax, cMin, cMax;
        public Color color;
    }

    public struct SheetState
    {
        public int rowMin, rowMax, colMin, colMax;
        public Vector3 offset;
        public bool moved;
        public Vector3 movedLocalPos;
        public Quaternion movedLocalRot;
    }

    public struct ColorState
    {
        public int rMin, rMax, cMin, cMax;
        public Color color;
    }

    public class EditSnapshot
    {
        public List<SheetState> sheets;
        public List<ColorState> colors;
    }

    private readonly List<Sheet> _sheets = new List<Sheet>();
    private readonly List<ColorOverride> _overrides = new List<ColorOverride>();
    private readonly Stack<SheetPiece> _piecePool = new Stack<SheetPiece>();
    private readonly List<Vector3> _colliderVerts = new List<Vector3>();
    private readonly List<int> _colliderTris = new List<int>();
    private Mesh _colliderMesh;
    private Transform _root;
    private Transform _poolRoot;
    private MeshRenderer _sheetRenderer;
    private Material _material;
    private float _grabYBounds = 1f;
    private bool _piecesGrabbable;

    public void SetGrabBounds(float yGrabBounds) => _grabYBounds = yGrabBounds;

    public void SetDataSource(DataSource source)
    {
        if (IsBaked) Unbake();
        dataSource = source;
        NotifyEditState();
    }

    public bool IsBaked => _root != null;

    public void SetPiecesPresented(bool presented)
    {
        if (_root != null) _root.gameObject.SetActive(presented);
    }

    public bool TryGetVisibleCellWorld(int visRow, int visCol, out Vector3 world)
    {
        world = Vector3.zero;
        if (!IsBaked || sheetGenerator == null) return false;

        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        if (colSp <= 0f || rowSp <= 0f) return false;

        Sheet r = SheetAt(visRow, visCol);
        if (r == null || r.piece == null) return false;

        float centerX = (r.colMin + r.colMax) * 0.5f * colSp;
        float centerZ = (r.rowMin + r.rowMax) * 0.5f * rowSp;
        Vector3 pieceLocal = new Vector3(visCol * colSp - centerX, sheetGenerator.SheetTopY, visRow * rowSp - centerZ);

        Vector3 lp = r.piece.transform.localPosition;
        Quaternion lr = r.piece.transform.localRotation;
        Vector3 local = lr * pieceLocal + lp;
        world = sheetGenerator.transform.TransformPoint(local);
        return true;
    }
    public IReadOnlyList<Sheet> Sheets => _sheets;
    public bool HasMultipleSheets => _sheets.Count > 1;
    public bool HasColorOverrides => _overrides.Count > 0;
    public int ColorOverrideCount => _overrides.Count;

    public bool HasMovedSheets
    {
        get
        {
            for (int i = 0; i < _sheets.Count; i++)
                if (_sheets[i].moved) return true;
            return false;
        }
    }

    public bool HasInvasiveEdits => HasMultipleSheets || HasColorOverrides || HasMovedSheets;

    private void Awake()
    {
        if (sheetGenerator == null) sheetGenerator = FindAnyObjectByType<SheetGenerator>();
        if (dataSource == null && sheetGenerator != null) dataSource = sheetGenerator.dataSource;
        if (dataSource == null) dataSource = DatasetManager.ActiveSource ?? FileReader.Instance;

        if (sheetGenerator != null)
        {
            sheetGenerator.OnSheetLayoutChanged += OnSheetLayoutChanged;
            sheetGenerator.SetSheetResolver(this);
        }
    }

    private void OnDestroy()
    {
        if (sheetGenerator != null)
        {
            sheetGenerator.OnSheetLayoutChanged -= OnSheetLayoutChanged;
            sheetGenerator.SetSheetResolver(null);
        }
        if (_colliderMesh != null) Destroy(_colliderMesh);
    }

    private void OnSheetLayoutChanged()
    {
        if (!IsBaked) return;

        int maxRow = sheetGenerator.RowCount - 1;
        int maxCol = sheetGenerator.VisibleColCount - 1;

        if (!HasInvasiveEdits && _sheets.Count == 1)
        {
            Sheet whole = _sheets[0];
            whole.rowMin = 0; whole.rowMax = maxRow;
            whole.colMin = 0; whole.colMax = maxCol;
            RebuildPiece(whole);
            SyncCollider();
            OnSheetsChanged?.Invoke();
            return;
        }

        int covMaxRow = -1, covMaxCol = -1;
        for (int i = 0; i < _sheets.Count; i++)
        {
            if (_sheets[i].rowMax > covMaxRow) covMaxRow = _sheets[i].rowMax;
            if (_sheets[i].colMax > covMaxCol) covMaxCol = _sheets[i].colMax;
        }

        if (covMaxRow != maxRow || covMaxCol != maxCol)
        {
            Unbake();
            OnEditsDroppedByReload?.Invoke();
            SheetNotices.EditsDropped(this,
                "The dataset changed shape, so its edits and compare pins were cleared.");
            return;
        }

        for (int i = 0; i < _sheets.Count; i++)
            RebuildPiece(_sheets[i]);
        SyncCollider();
        OnSheetsChanged?.Invoke();
    }

    public void EnsureBaked()
    {
        if (IsBaked) return;
        if (sheetGenerator == null || !sheetGenerator.MeshBuilt) return;

        _sheetRenderer = sheetGenerator.GetComponent<MeshRenderer>();
        _material = _sheetRenderer != null ? _sheetRenderer.sharedMaterial : null;

        GameObject rootObj = new GameObject("Sheets");
        _root = rootObj.transform;
        _root.SetParent(sheetGenerator.transform, false);
        if (!sheetGenerator.IsPresented) rootObj.SetActive(false);

        Sheet whole = new Sheet
        {
            rowMin = 0,
            rowMax = sheetGenerator.RowCount - 1,
            colMin = 0,
            colMax = sheetGenerator.VisibleColCount - 1,
            offset = Vector3.zero
        };
        _sheets.Add(whole);
        BuildPiece(whole);

        if (_sheetRenderer != null) _sheetRenderer.enabled = false;
        SyncCollider();
        OnSheetsChanged?.Invoke();
    }

    public void Unbake()
    {
        if (!IsBaked) return;

        for (int i = 0; i < _sheets.Count; i++)
            ReleasePiece(_sheets[i]);
        _sheets.Clear();
        _overrides.Clear();

        if (_root != null) Destroy(_root.gameObject);
        _root = null;

        if (_sheetRenderer != null)
            _sheetRenderer.enabled = sheetGenerator == null || sheetGenerator.IsPresented;
        if (sheetGenerator != null) sheetGenerator.RestoreColliderMesh();
        OnSheetsChanged?.Invoke();
        NotifyEditState();
    }

    private void NotifyEditState() => OnEditStateChanged?.Invoke();

    public EditSnapshot CaptureState()
    {
        if (!IsBaked || !HasInvasiveEdits) return null;
        CaptureMovedPoses();

        EditSnapshot snap = new EditSnapshot
        {
            sheets = new List<SheetState>(_sheets.Count),
            colors = new List<ColorState>(_overrides.Count)
        };

        for (int i = 0; i < _sheets.Count; i++)
        {
            Sheet s = _sheets[i];
            snap.sheets.Add(new SheetState
            {
                rowMin = s.rowMin, rowMax = s.rowMax, colMin = s.colMin, colMax = s.colMax,
                offset = s.offset,
                moved = s.moved, movedLocalPos = s.movedLocalPos, movedLocalRot = s.movedLocalRot
            });
        }

        for (int i = 0; i < _overrides.Count; i++)
        {
            ColorOverride o = _overrides[i];
            snap.colors.Add(new ColorState { rMin = o.rMin, rMax = o.rMax, cMin = o.cMin, cMax = o.cMax, color = o.color });
        }

        return snap;
    }

    public void RestoreState(EditSnapshot snap)
    {
        if (snap == null || snap.sheets == null || snap.sheets.Count == 0) return;

        EnsureBaked();
        if (!IsBaked) return;

        for (int i = 0; i < _sheets.Count; i++) ReleasePiece(_sheets[i]);
        _sheets.Clear();
        _overrides.Clear();

        for (int i = 0; i < snap.colors.Count; i++)
        {
            ColorState c = snap.colors[i];
            _overrides.Add(new ColorOverride { rMin = c.rMin, rMax = c.rMax, cMin = c.cMin, cMax = c.cMax, color = c.color });
        }

        for (int i = 0; i < snap.sheets.Count; i++)
        {
            SheetState ss = snap.sheets[i];
            Sheet sheet = new Sheet
            {
                rowMin = ss.rowMin, rowMax = ss.rowMax, colMin = ss.colMin, colMax = ss.colMax,
                offset = ss.offset,
                moved = ss.moved, movedLocalPos = ss.movedLocalPos, movedLocalRot = ss.movedLocalRot
            };
            _sheets.Add(sheet);
            BuildPiece(sheet);
        }

        RefreshAllColors();
        SyncCollider();
        OnSheetsChanged?.Invoke();
        NotifyEditState();
    }

    public Sheet SheetAt(int visRow, int visCol)
    {
        for (int i = 0; i < _sheets.Count; i++)
            if (_sheets[i].Contains(visRow, visCol)) return _sheets[i];
        return null;
    }

    public bool Slice(Sheet sheet, SliceAxis axis, int boundary, float gap, out SliceRecord record)
    {
        record = default;
        if (sheet == null || !_sheets.Contains(sheet)) return false;

        float half = gap * 0.5f;
        Sheet a, b;

        if (axis == SliceAxis.Column)
        {
            if (boundary < sheet.colMin || boundary > sheet.colMax - 1) return false;
            a = new Sheet { rowMin = sheet.rowMin, rowMax = sheet.rowMax, colMin = sheet.colMin, colMax = boundary, offset = sheet.offset + new Vector3(-half, 0f, 0f) };
            b = new Sheet { rowMin = sheet.rowMin, rowMax = sheet.rowMax, colMin = boundary + 1, colMax = sheet.colMax, offset = sheet.offset + new Vector3(half, 0f, 0f) };
        }
        else
        {
            if (boundary < sheet.rowMin || boundary > sheet.rowMax - 1) return false;
            a = new Sheet { rowMin = sheet.rowMin, rowMax = boundary, colMin = sheet.colMin, colMax = sheet.colMax, offset = sheet.offset + new Vector3(0f, 0f, -half) };
            b = new Sheet { rowMin = boundary + 1, rowMax = sheet.rowMax, colMin = sheet.colMin, colMax = sheet.colMax, offset = sheet.offset + new Vector3(0f, 0f, half) };
        }

        if (sheet.piece != null) CapturePose(sheet);
        record = new SliceRecord
        {
            pRowMin = sheet.rowMin, pRowMax = sheet.rowMax, pColMin = sheet.colMin, pColMax = sheet.colMax,
            pOffset = sheet.offset,
            pMoved = sheet.moved, pMovedPos = sheet.movedLocalPos, pMovedRot = sheet.movedLocalRot,
            aRowMin = a.rowMin, aRowMax = a.rowMax, aColMin = a.colMin, aColMax = a.colMax,
            bRowMin = b.rowMin, bRowMax = b.rowMax, bColMin = b.colMin, bColMax = b.colMax,
            axis = axis, boundary = boundary
        };

        InheritParentPose(sheet, a);
        InheritParentPose(sheet, b);

        int idx = _sheets.IndexOf(sheet);
        ReleasePiece(sheet);
        _sheets.RemoveAt(idx);
        _sheets.Add(a);
        _sheets.Add(b);
        BuildPiece(a);
        BuildPiece(b);

        SyncCollider();
        OnSheetsChanged?.Invoke();
        OnSliced?.Invoke(axis, boundary);
        NotifyEditState();
        return true;
    }

    public bool UndoSlice(SliceRecord r)
    {
        if (!IsBaked) return false;

        Sheet a = FindByBounds(r.aRowMin, r.aRowMax, r.aColMin, r.aColMax);
        Sheet b = FindByBounds(r.bRowMin, r.bRowMax, r.bColMin, r.bColMax);
        if (a == null || b == null) return false;

        ReleasePiece(a); _sheets.Remove(a);
        ReleasePiece(b); _sheets.Remove(b);

        Sheet parent = new Sheet
        {
            rowMin = r.pRowMin, rowMax = r.pRowMax, colMin = r.pColMin, colMax = r.pColMax,
            offset = r.pOffset,
            moved = r.pMoved, movedLocalPos = r.pMovedPos, movedLocalRot = r.pMovedRot
        };
        _sheets.Add(parent);
        BuildPiece(parent);

        SyncCollider();
        OnSheetsChanged?.Invoke();
        OnSliceUndone?.Invoke(parent.rowMin, parent.rowMax, parent.colMin, parent.colMax, r.axis, r.boundary);

        if (!HasInvasiveEdits) Unbake();
        else NotifyEditState();
        return true;
    }

    public void ResetSlices()
    {
        if (!IsBaked || !HasMultipleSheets) return;
        if (HasColorOverrides) MergeToWhole();
        else Unbake();
        OnSlicesReset?.Invoke();
    }

    public void AddColorOverride(Sheet sheet, Color color)
    {
        if (!IsBaked || sheet == null) return;
        _overrides.Add(new ColorOverride
        {
            rMin = sheet.rowMin,
            rMax = sheet.rowMax,
            cMin = sheet.colMin,
            cMax = sheet.colMax,
            color = color
        });
        RebuildPiece(sheet);
        NotifyEditState();
    }

    public void ResetColors()
    {
        if (!IsBaked) return;
        _overrides.Clear();
        if (HasInvasiveEdits) { RefreshAllColors(); NotifyEditState(); }
        else Unbake();
    }

    public bool UndoColorOverride()
    {
        if (!IsBaked || _overrides.Count == 0) return false;
        _overrides.RemoveAt(_overrides.Count - 1);
        if (HasInvasiveEdits) { RefreshAllColors(); NotifyEditState(); }
        else Unbake();
        return true;
    }

    public bool TryGetOverride(int visRow, int visCol, out Color color)
    {
        for (int i = _overrides.Count - 1; i >= 0; i--)
        {
            ColorOverride o = _overrides[i];
            if (visRow >= o.rMin && visRow <= o.rMax && visCol >= o.cMin && visCol <= o.cMax)
            {
                color = o.color;
                return true;
            }
        }
        color = default;
        return false;
    }

    private void MergeToWhole()
    {
        for (int i = 0; i < _sheets.Count; i++)
            ReleasePiece(_sheets[i]);
        _sheets.Clear();

        Sheet whole = new Sheet
        {
            rowMin = 0,
            rowMax = sheetGenerator.RowCount - 1,
            colMin = 0,
            colMax = sheetGenerator.VisibleColCount - 1,
            offset = Vector3.zero
        };
        _sheets.Add(whole);
        BuildPiece(whole);
        SyncCollider();
        OnSheetsChanged?.Invoke();
        NotifyEditState();
    }

    private void CapturePose(Sheet r)
    {
        if (r.piece == null) return;
        Vector3 basePos = BaseLocalPos(r);
        Vector3 cur = r.piece.transform.localPosition;
        Quaternion curRot = r.piece.transform.localRotation;
        r.moved = (cur - basePos).sqrMagnitude > 1e-6f || Quaternion.Angle(curRot, Quaternion.identity) > 0.05f;
        r.movedLocalPos = cur;
        r.movedLocalRot = curRot;
    }

    public bool CaptureMovedPoses()
    {
        bool before = HasMovedSheets;
        bool any = false;
        for (int i = 0; i < _sheets.Count; i++)
        {
            if (_sheets[i].piece == null) continue;
            CapturePose(_sheets[i]);
            if (_sheets[i].moved) any = true;
        }
        SyncCollider();
        if (HasMovedSheets != before) NotifyEditState();
        return any;
    }

    public void NotifyMoveCommitted(SheetPiece piece, Vector3 preLocalPos, Quaternion preLocalRot)
    {
        Sheet s = SheetForPiece(piece);
        if (s == null) return;
        CapturePose(s);
        SyncCollider();
        if (s.moved) NotifyEditState();
        OnSheetMoveCommitted?.Invoke(s, preLocalPos, preLocalRot);
    }

    public bool RestoreSheetPose(int rMin, int rMax, int cMin, int cMax, Vector3 pos, Quaternion rot)
    {
        if (!IsBaked) return false;
        Sheet s = FindByBounds(rMin, rMax, cMin, cMax);
        if (s == null || s.piece == null) return false;

        s.piece.transform.localPosition = pos;
        s.piece.transform.localRotation = rot;
        CapturePose(s);
        SyncCollider();
        NotifyEditState();
        return true;
    }

    private Sheet SheetForPiece(SheetPiece piece)
    {
        for (int i = 0; i < _sheets.Count; i++)
            if (_sheets[i].piece == piece) return _sheets[i];
        return null;
    }

    private Sheet FindByBounds(int rMin, int rMax, int cMin, int cMax)
    {
        for (int i = 0; i < _sheets.Count; i++)
        {
            Sheet s = _sheets[i];
            if (s.rowMin == rMin && s.rowMax == rMax && s.colMin == cMin && s.colMax == cMax) return s;
        }
        return null;
    }

    public void ResetGrabs()
    {
        if (!IsBaked) return;
        for (int i = 0; i < _sheets.Count; i++)
        {
            Sheet r = _sheets[i];
            r.moved = false;
            if (r.piece != null)
            {
                r.piece.transform.localPosition = BaseLocalPos(r);
                r.piece.transform.localRotation = Quaternion.identity;
            }
        }
        SyncCollider();
        NotifyEditState();
    }

    public void SetPiecesGrabbable(bool on)
    {
        _piecesGrabbable = on;
        for (int i = 0; i < _sheets.Count; i++)
            if (_sheets[i].piece != null) _sheets[i].piece.SetGrabbable(on);
    }

    public Vector3 ResolveGrabPosition(SheetPiece piece, Vector3 lastValid, Vector3 candidate)
    {
        if (sheetGenerator == null || piece == null) return candidate;

        Sheet moving = SheetForPiece(piece);
        if (moving == null) return candidate;

        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        if (colSp <= 0f || rowSp <= 0f) return candidate;

        GetExtents(moving, piece.transform.localRotation.eulerAngles.y, out float mhx, out float mhz);
        float mhy = sheetGenerator.SheetTopY * 0.5f;
        float lvYc = lastValid.y + mhy;

        float x = candidate.x;
        float z = candidate.z;
        float yc = candidate.y + mhy;

        for (int i = 0; i < _sheets.Count; i++)
        {
            if (!TryBox(_sheets[i], moving, out Vector3 c, out float shx, out float shz, out float shy)) continue;
            if (Mathf.Abs(lvYc - c.y) >= mhy + shy) continue;
            if (Mathf.Abs(lastValid.z - c.z) >= mhz + shz) continue;
            float gapX = mhx + shx;
            if (lastValid.x <= c.x) x = Mathf.Min(x, c.x - gapX);
            else x = Mathf.Max(x, c.x + gapX);
        }

        for (int i = 0; i < _sheets.Count; i++)
        {
            if (!TryBox(_sheets[i], moving, out Vector3 c, out float shx, out float shz, out float shy)) continue;
            if (Mathf.Abs(lvYc - c.y) >= mhy + shy) continue;
            if (Mathf.Abs(x - c.x) >= mhx + shx) continue;
            float gapZ = mhz + shz;
            if (lastValid.z <= c.z) z = Mathf.Min(z, c.z - gapZ);
            else z = Mathf.Max(z, c.z + gapZ);
        }

        for (int i = 0; i < _sheets.Count; i++)
        {
            if (!TryBox(_sheets[i], moving, out Vector3 c, out float shx, out float shz, out float shy)) continue;
            if (Mathf.Abs(x - c.x) >= mhx + shx) continue;
            if (Mathf.Abs(z - c.z) >= mhz + shz) continue;
            float gapY = mhy + shy;
            if (lvYc <= c.y) yc = Mathf.Min(yc, c.y - gapY);
            else yc = Mathf.Max(yc, c.y + gapY);
        }

        return new Vector3(x, yc - mhy, z);
    }

    public void SettlePiece(SheetPiece piece)
    {
        if (sheetGenerator == null || piece == null) return;
        Sheet moving = SheetForPiece(piece);
        if (moving == null) return;

        GetExtents(moving, piece.transform.localRotation.eulerAngles.y, out float mhx, out float mhz);
        float mhy = sheetGenerator.SheetTopY * 0.5f;

        for (int pass = 0; pass < 4; pass++)
        {
            bool any = false;
            Vector3 pos = piece.transform.localPosition;
            for (int i = 0; i < _sheets.Count; i++)
            {
                if (!TryBox(_sheets[i], moving, out Vector3 c, out float shx, out float shz, out float shy)) continue;
                float dx = pos.x - c.x;
                float dy = (pos.y + mhy) - c.y;
                float dz = pos.z - c.z;
                float penX = (mhx + shx) - Mathf.Abs(dx);
                float penY = (mhy + shy) - Mathf.Abs(dy);
                float penZ = (mhz + shz) - Mathf.Abs(dz);
                if (penX <= 0f || penY <= 0f || penZ <= 0f) continue;

                if (penX <= penY && penX <= penZ)
                    pos.x += dx >= 0f ? penX : -penX;
                else if (penZ <= penY)
                    pos.z += dz >= 0f ? penZ : -penZ;
                else
                    pos.y += dy >= 0f ? penY : -penY;
                any = true;
            }
            piece.transform.localPosition = pos;
            if (!any) break;
        }
    }

    public bool RotationCausesOverlap(SheetPiece piece, Quaternion candidateLocalRot)
    {
        if (sheetGenerator == null || piece == null) return false;
        Sheet moving = SheetForPiece(piece);
        if (moving == null) return false;

        GetExtents(moving, candidateLocalRot.eulerAngles.y, out float mhx, out float mhz);
        float mhy = sheetGenerator.SheetTopY * 0.5f;
        Vector3 p = piece.transform.localPosition;
        float myc = p.y + mhy;

        for (int i = 0; i < _sheets.Count; i++)
        {
            if (!TryBox(_sheets[i], moving, out Vector3 c, out float shx, out float shz, out float shy)) continue;
            if (Mathf.Abs(p.x - c.x) >= mhx + shx) continue;
            if (Mathf.Abs(p.z - c.z) >= mhz + shz) continue;
            if (Mathf.Abs(myc - c.y) >= mhy + shy) continue;
            return true;
        }
        return false;
    }

    private void GetExtents(Sheet s, float yawDeg, out float ehx, out float ehz)
    {
        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        float bx = CellSpan(s.colMin, s.colMax) * colSp * 0.5f;
        float bz = CellSpan(s.rowMin, s.rowMax) * rowSp * 0.5f;
        float r = yawDeg * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(r));
        float sn = Mathf.Abs(Mathf.Sin(r));
        ehx = c * bx + sn * bz;
        ehz = sn * bx + c * bz;
    }

    private bool TryBox(Sheet s, Sheet moving, out Vector3 center, out float hx, out float hz, out float hy)
    {
        center = default;
        hx = hz = hy = 0f;
        if (s == moving || s.piece == null) return false;
        Transform t = s.piece.transform;
        GetExtents(s, t.localRotation.eulerAngles.y, out hx, out hz);
        hy = sheetGenerator.SheetTopY * 0.5f;
        Vector3 p = t.localPosition;
        center = new Vector3(p.x, p.y + hy, p.z);
        return true;
    }

    private static int CellSpan(int min, int max) => max - min + 1;

    private Vector3 BaseLocalPos(Sheet r)
    {
        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        float cx = (r.colMin + r.colMax) * 0.5f * colSp;
        float cz = (r.rowMin + r.rowMax) * 0.5f * rowSp;
        return new Vector3(cx, 0f, cz) + r.offset;
    }

    private void BuildPiece(Sheet r)
    {
        SheetPiece piece = AcquirePiece();
        piece.transform.SetParent(_root, false);
        piece.gameObject.SetActive(true);
        piece.Build(sheetGenerator, dataSource, this, _material, r.rowMin, r.rowMax, r.colMin, r.colMax, r.offset, _grabYBounds);

        ApplyPose(r, piece);
        piece.SetGrabbable(_piecesGrabbable);
        r.piece = piece;
    }

    private SheetPiece AcquirePiece()
    {
        while (_piecePool.Count > 0)
        {
            SheetPiece pooled = _piecePool.Pop();
            if (pooled != null) return pooled;
        }

        GameObject go = piecePrefab != null ? Instantiate(piecePrefab) : new GameObject("SheetPiece");
        go.name = "SheetPiece";
        SheetPiece piece = go.GetComponent<SheetPiece>();
        if (piece == null) piece = go.AddComponent<SheetPiece>();
        return piece;
    }

    private void ReleasePiece(Sheet r)
    {
        if (r.piece == null) return;

        if (_poolRoot == null)
        {
            GameObject poolObj = new GameObject("SheetPiecePool");
            _poolRoot = poolObj.transform;
            _poolRoot.SetParent(transform, false);
        }

        r.piece.SetGrabbable(false);
        r.piece.gameObject.SetActive(false);
        r.piece.transform.SetParent(_poolRoot, false);
        _piecePool.Push(r.piece);
        r.piece = null;
    }

    private void RebuildPiece(Sheet r)
    {
        if (r.piece == null) { BuildPiece(r); return; }
        r.piece.Build(sheetGenerator, dataSource, this, _material, r.rowMin, r.rowMax, r.colMin, r.colMax, r.offset, _grabYBounds);
        ApplyPose(r, r.piece);
        r.piece.SetGrabbable(_piecesGrabbable);
    }

    private void ApplyPose(Sheet r, SheetPiece piece)
    {
        if (!r.moved) return;
        piece.transform.localPosition = r.movedLocalPos;
        piece.transform.localRotation = r.movedLocalRot;
    }

    private void InheritParentPose(Sheet parent, Sheet child)
    {
        if (!parent.moved) return;
        Vector3 parentBase = BaseLocalPos(parent);
        Vector3 childBase = BaseLocalPos(child);
        child.moved = true;
        child.movedLocalRot = parent.movedLocalRot;
        child.movedLocalPos = parent.movedLocalPos + parent.movedLocalRot * (childBase - parentBase);
    }

    private void RefreshAllColors()
    {
        for (int i = 0; i < _sheets.Count; i++)
        {
            Sheet r = _sheets[i];
            RebuildPiece(r);
        }
    }

    private void SyncCollider()
    {
        if (sheetGenerator == null) return;

        _colliderVerts.Clear();
        _colliderTris.Clear();
        for (int i = 0; i < _sheets.Count; i++)
            if (_sheets[i].piece != null)
                _sheets[i].piece.AppendCollider(_colliderVerts, _colliderTris);

        if (_colliderVerts.Count == 0)
        {
            sheetGenerator.RestoreColliderMesh();
            return;
        }

        if (_colliderMesh == null)
        {
            _colliderMesh = new Mesh { name = "SheetCollider" };
            _colliderMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        _colliderMesh.Clear();
        _colliderMesh.SetVertices(_colliderVerts);
        _colliderMesh.SetTriangles(_colliderTris, 0, false);
        sheetGenerator.SetColliderMesh(_colliderMesh);
    }

    public bool TryResolveVisibleCell(Vector3 world, out int visRow, out int visCol)
    {
        visRow = -1;
        visCol = -1;
        if (!IsBaked || sheetGenerator == null) return false;

        float colSp = sheetGenerator.CurrentColumnSpacing;
        float rowSp = sheetGenerator.CurrentRowSpacing;
        if (colSp <= 0f || rowSp <= 0f) return false;

        Vector3 local = sheetGenerator.transform.InverseTransformPoint(world);
        for (int i = 0; i < _sheets.Count; i++)
        {
            Sheet r = _sheets[i];
            if (r.piece == null) continue;

            Vector3 lp = r.piece.transform.localPosition;
            Quaternion lr = r.piece.transform.localRotation;
            Vector3 pieceLocal = Quaternion.Inverse(lr) * (local - lp);
            float centerX = (r.colMin + r.colMax) * 0.5f * colSp;
            float centerZ = (r.rowMin + r.rowMax) * 0.5f * rowSp;
            int vc = Mathf.RoundToInt((pieceLocal.x + centerX) / colSp);
            int vr = Mathf.RoundToInt((pieceLocal.z + centerZ) / rowSp);

            if (r.Contains(vr, vc))
            {
                visRow = vr;
                visCol = vc;
                return true;
            }
        }
        return false;
    }
}
