using UnityEngine;

// Fica no assembly Roguelike (pasta Monsters) para a barra do chefe também usar; o resto do jogo referencia esse assembly.
// Escala única da interface do jogo: a HUD é desenhada numa tela virtual de ReferenceHeight pixels de altura
// e ampliada com GUI.matrix, então textos, barras e painéis crescem juntos em telas maiores.
public static class HudScale
{
  public const float ReferenceHeight = 640f;

  public static float Factor => Mathf.Max(1f, Screen.height / ReferenceHeight);
  public static float Width => Screen.width / Factor;
  public static float Height => Screen.height / Factor;

  public static void Begin()
  {
    float factor = Factor;
    GUI.matrix = Matrix4x4.Scale(new Vector3(factor, factor, 1f));
  }

  public static void End()
  {
    GUI.matrix = Matrix4x4.identity;
  }

  // Ponto de Camera.WorldToScreenPoint (pixels, origem embaixo) na tela virtual da GUI (origem em cima)
  public static Vector2 FromScreenPoint(Vector3 screen)
  {
    float factor = Factor;
    return new Vector2(screen.x / factor, (Screen.height - screen.y) / factor);
  }

  // Maior fonte (até maxSize) com que o texto cabe no retângulo; com wordWrap, quebra as linhas antes de diminuir
  public static void FitFont(GUIStyle style, string text, Rect rect, int maxSize, int minSize = 8)
  {
    GUIContent content = new GUIContent(text);
    int size = Mathf.Max(minSize, maxSize);
    for (; size > minSize; size--)
    {
      style.fontSize = size;
      if (Fits(style, content, rect)) return;
    }
    style.fontSize = minSize;
  }

  private static bool Fits(GUIStyle style, GUIContent content, Rect rect)
  {
    if (style.wordWrap)
    {
      if (style.CalcHeight(content, rect.width) > rect.height) return false;
      // uma palavra sozinha mais larga que a caixa também estoura
      foreach (string word in content.text.Split(' ', '\n'))
      {
        if (style.CalcSize(new GUIContent(word)).x > rect.width) return false;
      }
      return true;
    }

    Vector2 size = style.CalcSize(content);
    return size.x <= rect.width && size.y <= rect.height;
  }
}
