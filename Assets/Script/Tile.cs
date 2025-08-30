using UnityEngine;

public class Tile : MonoBehaviour
{
    public Vector2Int gridPos;
    public bool isOccupied = false;
    public GameObject currentOccupant;

    private SpriteRenderer spriteRenderer;
    private Color defaultColor;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultColor = spriteRenderer.color;
    }

    public void SetOccupant(GameObject occupant)
{
    currentOccupant = occupant;
    isOccupied = occupant != null;
}

    public void Highlight(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    public void ResetHighlight()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = defaultColor;
        }
    }
}
