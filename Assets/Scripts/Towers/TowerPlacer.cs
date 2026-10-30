using UnityEngine;
using UnityEngine.InputSystem;

public class TowerPlacer : MonoBehaviour
{
    [SerializeField] private TowerDefinition[] towerDefinitions;
    [SerializeField] private PlacementGrid grid;
    [SerializeField] private Transform player;

    private static readonly Color ValidColor = new Color(0.25f, 0.9f, 0.35f);
    private static readonly Color InvalidColor = new Color(0.9f, 0.25f, 0.25f);

    public static bool IsPlacing { get; private set; }

    public TowerDefinition[] Definitions => towerDefinitions;
    public int SelectedIndex { get; private set; } = -1;

    private GameObject previewMarker;
    private Material previewMaterial;
    private Vector2Int hoveredCell;
    private bool hoveredCellValid;

    private void Awake()
    {
        SelectedIndex = -1;
        IsPlacing = false;
    }

    private void OnDestroy()
    {
        if (previewMarker != null) Destroy(previewMarker);
        if (previewMaterial != null) Destroy(previewMaterial);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        HandleSelectionInput(kb);

        if (!IsPlacing)
        {
            SetPreviewActive(false);
            return;
        }

        UpdateHoveredCell();
        UpdatePreview();

        // Taps/clicks arrive through TouchControls.TryPlaceAtScreenPoint; B places at the facing cell.
        if (kb != null && kb.bKey.wasPressedThisFrame)
        {
            TryPlaceTower();
        }
    }

    public bool TryPlaceAtScreenPoint(Vector2 screenPoint)
    {
        if (!TryGetGroundCell(screenPoint, out Vector2Int cell)) return false;

        hoveredCell = cell;
        hoveredCellValid = IsCellValid(cell);
        UpdatePreview();

        if (!TryPlaceTower()) return false;

        SelectTower(-1);
        return true;
    }

    private bool TryGetGroundCell(Vector2 screenPoint, out Vector2Int cell)
    {
        cell = default;
        Camera cam = Camera.main;
        if (cam == null || grid == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPoint);
        var groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out float enter)) return false;

        cell = grid.WorldToCell(ray.GetPoint(enter));
        return true;
    }

    public void SelectTower(int index)
    {
        if (towerDefinitions == null || index < 0 || index >= towerDefinitions.Length)
        {
            SelectedIndex = -1;
            IsPlacing = false;
            if (grid != null) grid.ShowGrid = false;
            SetPreviewActive(false);
            return;
        }

        SelectedIndex = index;
        IsPlacing = true;
        if (grid != null) grid.ShowGrid = true;
        EnsurePreviewMarker();
    }

    private void HandleSelectionInput(Keyboard kb)
    {
        if (kb != null && towerDefinitions != null)
        {
            for (int i = 0; i < towerDefinitions.Length && i < 9; i++)
            {
                Key key = (Key)((int)Key.Digit1 + i);
                if (kb[key].wasPressedThisFrame)
                {
                    SelectTower(SelectedIndex == i ? -1 : i);
                }
            }

            if (kb.escapeKey.wasPressedThisFrame)
            {
                SelectTower(-1);
            }
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            SelectTower(-1);
        }
    }

    private void UpdateHoveredCell()
    {
        hoveredCellValid = false;
        if (grid == null || player == null) return;
        if (SelectedIndex < 0 || SelectedIndex >= towerDefinitions.Length) return;

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPoint = player.position + forward * grid.CellSize;
        hoveredCell = grid.WorldToCell(targetPoint);

        // With a mouse (desktop testing), preview the cell under the cursor instead.
        if (Touchscreen.current == null && Mouse.current != null
            && TryGetGroundCell(Mouse.current.position.ReadValue(), out Vector2Int mouseCell))
        {
            hoveredCell = mouseCell;
        }

        hoveredCellValid = IsCellValid(hoveredCell);
    }

    private bool IsCellValid(Vector2Int cell)
    {
        if (SelectedIndex < 0 || SelectedIndex >= towerDefinitions.Length) return false;

        TowerDefinition def = towerDefinitions[SelectedIndex];
        return grid.IsInBounds(cell)
            && !grid.IsOccupied(cell)
            && GameManager.Instance != null
            && GameManager.Instance.Scrap >= def.cost;
    }

    private void UpdatePreview()
    {
        if (previewMarker == null || grid == null) return;

        SetPreviewActive(true);
        previewMarker.transform.position = grid.CellToWorldCenter(hoveredCell) + Vector3.up * 0.05f;
        previewMaterial.color = hoveredCellValid ? ValidColor : InvalidColor;
    }

    private bool TryPlaceTower()
    {
        if (SelectedIndex < 0 || grid == null) return false;
        if (!hoveredCellValid)
        {
            Debug.Log("[TowerPlacer] Cannot place tower there.");
            return false;
        }

        TowerDefinition def = towerDefinitions[SelectedIndex];
        if (def.prefab == null)
        {
            Debug.LogWarning("[TowerPlacer] Selected tower definition has no prefab assigned - re-run 'Tools > Iron Hold > 2) Build Prototype Scene'.");
            return false;
        }

        if (GameManager.Instance == null || !GameManager.Instance.SpendScrap(def.cost)) return false;

        Vector3 spawnPos = grid.CellToWorldCenter(hoveredCell);
        Tower tower = Instantiate(def.prefab, spawnPos, Quaternion.identity);
        tower.gameObject.AddComponent<PopIn>();
        grid.Occupy(hoveredCell);

        GameFx.Dust(spawnPos);
        IsoCameraFollow.AddTrauma(0.15f);
        Sfx.Play(SfxId.Build);
        Debug.Log($"[TowerPlacer] Placed {def.displayName} at {spawnPos}");
        return true;
    }

    private void EnsurePreviewMarker()
    {
        if (previewMarker != null) return;

        previewMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        previewMarker.name = "TowerPlacementPreview";

        Collider col = previewMarker.GetComponent<Collider>();
        if (col != null) Destroy(col);

        float size = grid != null ? grid.CellSize : 3f;
        previewMarker.transform.localScale = new Vector3(size * 0.9f, 0.15f, size * 0.9f);

        Renderer renderer = previewMarker.GetComponent<Renderer>();
        previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        renderer.material = previewMaterial;

        SetPreviewActive(false);
    }

    private void SetPreviewActive(bool active)
    {
        if (previewMarker != null) previewMarker.SetActive(active);
    }
}
