using Fusion;
using UnityEngine;

public class NetworkCarVisualColor : NetworkBehaviour
{
    [Header("Renderer")]
    [SerializeField] private MeshRenderer bodyRenderer;

    [Header("Material Slot")]
    [SerializeField] private int colorMaterialIndex = 0;

    [Header("Player Colors")]
    [SerializeField] private Color[] playerColors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow
    };

    [Networked] private int ColorIndex { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ColorIndex = GetColorIndexFromPlayer(Object.InputAuthority);
        }

        ApplyColor();
    }

    public override void Render()
    {
        ApplyColor();
    }

    private int GetColorIndexFromPlayer(PlayerRef player)
    {
        if (Runner == null)
        {
            return 0;
        }

        int index = 0;

        foreach (PlayerRef currentPlayer in Runner.ActivePlayers)
        {
            if (currentPlayer == player)
            {
                return index % playerColors.Length;
            }

            index++;
        }

        return 0;
    }

    private void ApplyColor()
    {
        if (bodyRenderer == null || playerColors == null || playerColors.Length == 0)
        {
            return;
        }

        int safeColorIndex = Mathf.Abs(ColorIndex) % playerColors.Length;

        Material[] materials = bodyRenderer.materials;

        if (colorMaterialIndex < 0 || colorMaterialIndex >= materials.Length)
        {
            return;
        }

        materials[colorMaterialIndex].color = playerColors[safeColorIndex];
        bodyRenderer.materials = materials;
    }
}