using System.Collections.Generic;
using UnityEngine;

public static class LightField
{
  private const float RefreshInterval = 0.2f;

  private static Light[] lights = new Light[0];
  private static float lastRefreshTime = float.NegativeInfinity;
  private static readonly HashSet<Light> ignoredLights = new HashSet<Light>();

  public static void Ignore(Light light)
  {
    ignoredLights.Add(light);
  }

  public static float Sample(Vector3 position, out Vector3 awayDirection)
  {
    Refresh();

    float total = 0f;
    Vector3 away = Vector3.zero;
    foreach (Light light in lights)
    {
      if (light == null || !light.isActiveAndEnabled || light.type == LightType.Directional || light.intensity <= 0f
        || ignoredLights.Contains(light))
      {
        continue;
      }

      Vector3 offset = position - light.transform.position;
      float distance = offset.magnitude;
      if (distance >= light.range)
      {
        continue;
      }

      float falloff = 1f - distance / light.range;
      float contribution = light.intensity * falloff * falloff;
      total += contribution;

      offset.y = 0f;
      away += offset.normalized * contribution;
    }

    awayDirection = away.normalized;
    return total;
  }

  private static void Refresh()
  {
    if (Mathf.Abs(Time.time - lastRefreshTime) < RefreshInterval)
    {
      return;
    }

    lastRefreshTime = Time.time;
    lights = Object.FindObjectsByType<Light>();
    ignoredLights.RemoveWhere(light => light == null);
  }
}
