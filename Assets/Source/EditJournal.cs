using System;
using System.Collections.Generic;

public class EditJournal
{
    public enum Kind { Slice, Move, Color, Pin }

    public class Record
    {
        public Kind kind;
        public SheetManager.SliceRecord slice;
        public GrabTool.MoveRecord move;
        public bool pinIsSheet;
        public int pinORowMin, pinORowMax, pinOColMin, pinOColMax;
        public int pinRow, pinCol;
        public string pinColumnTitle, pinRowTitle;
    }

    private readonly List<Record> _records = new List<Record>();

    public int Count => _records.Count;

    public Record Peek() => _records.Count > 0 ? _records[_records.Count - 1] : null;

    public Record Pop()
    {
        if (_records.Count == 0) return null;
        Record r = _records[_records.Count - 1];
        _records.RemoveAt(_records.Count - 1);
        return r;
    }

    public void PushSlice(SheetManager.SliceRecord slice) =>
        _records.Add(new Record { kind = Kind.Slice, slice = slice });

    public void PushMove(GrabTool.MoveRecord move) =>
        _records.Add(new Record { kind = Kind.Move, move = move });

    public void PushColor() =>
        _records.Add(new Record { kind = Kind.Color });

    public void PushSheetPin(int oRowMin, int oRowMax, int oColMin, int oColMax) =>
        _records.Add(new Record
        {
            kind = Kind.Pin,
            pinIsSheet = true,
            pinORowMin = oRowMin, pinORowMax = oRowMax,
            pinOColMin = oColMin, pinOColMax = oColMax
        });

    public void PushCellPin(int row, int col, string columnTitle, string rowTitle) =>
        _records.Add(new Record
        {
            kind = Kind.Pin,
            pinIsSheet = false,
            pinRow = row, pinCol = col,
            pinColumnTitle = columnTitle, pinRowTitle = rowTitle
        });

    public void Clear() => _records.Clear();

    public void DropSheetEdits() => _records.RemoveAll(r => r.kind != Kind.Pin);

    public void PrunePins(Func<Record, bool> alive) =>
        _records.RemoveAll(r => r.kind == Kind.Pin && !alive(r));

    public List<Record> Capture() => _records.Count > 0 ? new List<Record>(_records) : null;

    public void Restore(List<Record> records)
    {
        _records.Clear();
        if (records != null) _records.AddRange(records);
    }

    public static string KindName(Kind kind)
    {
        switch (kind)
        {
            case Kind.Slice: return "slice";
            case Kind.Move: return "move";
            case Kind.Color: return "color";
            default: return "pin";
        }
    }
}
