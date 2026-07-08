using UnityEngine;

public class DatabaseSheetGenerator : SheetGenerator
{
    [Tooltip("Fixed length and width of every cell, shared by all tabs in the scene. Cells are square, so one value sets both X and Z. Stays fixed as sheets grow.")]
    public float cellXAndZ = 0.4f;

    protected override float ResolveCellSize(int totalRows, int totalCols) => cellXAndZ;
}
