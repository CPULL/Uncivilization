using UnityEngine;
using System.Collections.Generic;

namespace UnityEngine.UI {
  [AddComponentMenu("Layout/Grid Layout Group", 152)]
  public class CitiesGridLayout : LayoutGroup {
    public enum Axis { Horizontal = 0, Vertical = 1 }

    [SerializeField] protected Vector2 m_CellSize = new Vector2(100, 100);
    public Vector2 cellSize { get { return m_CellSize; } set { SetProperty(ref m_CellSize, value); } }

    [SerializeField] protected Vector2 m_Spacing = Vector2.zero;
    public Vector2 spacing { get { return m_Spacing; } set { SetProperty(ref m_Spacing, value); } }

    [SerializeField] protected Vector2 m_Inner = Vector2.zero;
    public Vector2 inner { get { return m_Inner; } set { SetProperty(ref m_Inner, value); } }

    [SerializeField] protected Vector2 m_Border = Vector2.zero;
    public Vector2 border { get { return m_Border; } set { SetProperty(ref m_Border, value); } }


    protected CitiesGridLayout() { }

#if UNITY_EDITOR
    protected override void OnValidate() {
      base.OnValidate();
    }

#endif

    public override void CalculateLayoutInputHorizontal() {
      base.CalculateLayoutInputHorizontal();

      int minColumns = 9;

      SetLayoutInputForAxis(
          padding.horizontal + (cellSize.x + spacing.x) * minColumns - spacing.x,
          padding.horizontal + (cellSize.x + spacing.x) * minColumns - spacing.x,
          -1, 0);
    }

    public override void CalculateLayoutInputVertical() {
      int minRows = 6;
      float minSpace = padding.vertical + (cellSize.y + spacing.y) * minRows - spacing.y;
      SetLayoutInputForAxis(minSpace, minSpace, -1, 1);
    }

    public override void SetLayoutHorizontal() {
      SetCellsAlongAxis(0);
    }

    public override void SetLayoutVertical() {
      SetCellsAlongAxis(1);
    }

    private void SetCellsAlongAxis(int axis) {
      // Normally a Layout Controller should only set horizontal values when invoked for the horizontal axis and only vertical values when invoked for the vertical axis.
      // However, in this case we set both the horizontal and vertical position when invoked for the vertical axis.
      // Since we only set the horizontal position and not the size, it shouldn't affect children's layout,
      // and thus shouldn't break the rule that all horizontal layout must be calculated before all vertical layout.

      if (axis == 0) {
        // Only set the sizes when invoked for horizontal axis, not the positions.
        for (int i = 0; i < rectChildren.Count; i++) {
          RectTransform rect = rectChildren[i];
          m_Tracker.Add(this, rect, DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDelta);
          rect.anchorMin = Vector2.up;
          rect.anchorMax = Vector2.up;
          rect.sizeDelta = cellSize;
        }
        return;
      }

      float width = rectTransform.rect.size.x;
      float height = rectTransform.rect.size.y;

      int cellCountX = 9;
      int cellCountY = 6;

      int cellsPerMainAxis, actualCellCountX, actualCellCountY;
      cellsPerMainAxis = cellCountX;
      actualCellCountX = Mathf.Clamp(cellCountX, 1, rectChildren.Count);
      actualCellCountY = Mathf.Clamp(cellCountY, 1, Mathf.CeilToInt(rectChildren.Count / (float)cellsPerMainAxis));

      Vector2 requiredSpace = new Vector2(
              actualCellCountX * cellSize.x + (actualCellCountX - 1) * spacing.x,
              actualCellCountY * cellSize.y + (actualCellCountY - 1) * spacing.y
              );
      Vector2 startOffset = new Vector2(
              GetStartOffset(0, requiredSpace.x),
              GetStartOffset(1, requiredSpace.y)
              );
      startOffset += border;

      for (int i = 0; i < rectChildren.Count; i++) {
        // Calculate the position based on the starting point and the size
        int positionX = i % 9;
        int positionY = i / 9;


        float offx = 0;
        float offy = 0;
        if (positionX > 2) offx += inner[0];
        if (positionX > 5) offx += inner[0];
        if (positionY > 2) offy += inner[1];

        SetChildAlongAxis(rectChildren[i], 0, startOffset.x + (cellSize[0] + spacing[0]) * positionX + offx, cellSize[0]);
        SetChildAlongAxis(rectChildren[i], 1, startOffset.y + (cellSize[1] + spacing[1]) * positionY + offy, cellSize[1]);
      }
    }
  }
}
