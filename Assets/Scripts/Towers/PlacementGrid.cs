using System.Collections.Generic;
using UnityEngine;

public class PlacementGrid : MonoBehaviour
{
    [SerializeField] private float cellSize = 3f;
    [SerializeField] private Vector2Int gridSize = new Vector2Int(20, 20);
    [SerializeField] private Vector3 origin = Vector3.zero;
    [SerializeField] private Color gridColor = new Color(0.2f, 1f, 1f, 0.6f);

    // Above the arena floor layers (paths/plaza/portals) so the grid stays visible.
    private const float LineHeight = 0.1f;

    private readonly HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();
    private Material lineMaterial;

    public float CellSize => cellSize;
    public bool ShowGrid { get; set; }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        Vector3 local = worldPos - origin;
        int x = Mathf.FloorToInt(local.x / cellSize + gridSize.x * 0.5f);
        int z = Mathf.FloorToInt(local.z / cellSize + gridSize.y * 0.5f);
        return new Vector2Int(x, z);
    }

    public Vector3 CellToWorldCenter(Vector2Int cell)
    {
        float x = (cell.x - gridSize.x * 0.5f + 0.5f) * cellSize;
        float z = (cell.y - gridSize.y * 0.5f + 0.5f) * cellSize;
        return origin + new Vector3(x, 0f, z);
    }

    public bool IsInBounds(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;
    }

    public bool IsOccupied(Vector2Int cell) => occupiedCells.Contains(cell);

    public void Occupy(Vector2Int cell) => occupiedCells.Add(cell);

    public void Free(Vector2Int cell) => occupiedCells.Remove(cell);

    public void OccupyWorldArea(Vector3 worldCenter, Vector3 size)
    {
        Vector2Int minCell = WorldToCell(worldCenter - new Vector3(size.x, 0f, size.z) * 0.5f);
        Vector2Int maxCell = WorldToCell(worldCenter + new Vector3(size.x, 0f, size.z) * 0.5f);

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                Occupy(new Vector2Int(x, z));
            }
        }
    }

    private void OnRenderObject()
    {
        if (!ShowGrid) return;
        EnsureMaterial();

        lineMaterial.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINES);
        GL.Color(gridColor);

        float halfWidth = gridSize.x * cellSize * 0.5f;
        float halfHeight = gridSize.y * cellSize * 0.5f;

        for (int x = 0; x <= gridSize.x; x++)
        {
            float wx = origin.x - halfWidth + x * cellSize;
            GL.Vertex(new Vector3(wx, origin.y + LineHeight, origin.z - halfHeight));
            GL.Vertex(new Vector3(wx, origin.y + LineHeight, origin.z + halfHeight));
        }

        for (int z = 0; z <= gridSize.y; z++)
        {
            float wz = origin.z - halfHeight + z * cellSize;
            GL.Vertex(new Vector3(origin.x - halfWidth, origin.y + LineHeight, wz));
            GL.Vertex(new Vector3(origin.x + halfWidth, origin.y + LineHeight, wz));
        }

        GL.End();
        GL.PopMatrix();
    }

    private void EnsureMaterial()
    {
        if (lineMaterial != null) return;

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
