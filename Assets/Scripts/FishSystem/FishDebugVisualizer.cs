using System.Text;
using UnityEngine;

/// <summary>
/// Optional development-only overlay for professional fish movement and spawn flow.
/// Keep this component disabled in release builds unless a live diagnostics overlay is desired.
/// </summary>
[DisallowMultipleComponent]
public sealed class FishDebugVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SwapFishScript levelDirector;
    [SerializeField] private FishSpawnDirectorService spawnDirector;

    [Header("World Gizmos")]
    [SerializeField] private bool drawVisibleBounds = true;
    [SerializeField] private bool drawPaddedBounds = true;
    [SerializeField] private bool drawCenterArea = true;
    [SerializeField] private bool drawSpawnAndExitPoints = true;
    [SerializeField] private bool drawFishRoutes = true;
    [SerializeField] private bool drawSpacingRadii = true;
    [SerializeField, Range(0.05f, 0.30f)] private float viewportPadding = 0.05f;
    [SerializeField, Range(0.05f, 0.45f)] private float centerHalfWidth = 0.22f;
    [SerializeField, Range(0.05f, 0.45f)] private float centerHalfHeight = 0.22f;

    [Header("Runtime Overlay")]
    [SerializeField] private bool drawRuntimeOverlay = true;
    [SerializeField] private Vector2 overlayPosition = new Vector2(12f, 90f);
    [SerializeField, Min(220f)] private float overlayWidth = 360f;

    private readonly Vector3[] routeBuffer = new Vector3[8];
    private readonly StringBuilder overlayText = new StringBuilder(512);
    private GUIStyle overlayStyle;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (levelDirector == null)
        {
            levelDirector = GetComponent<SwapFishScript>();
        }

        if (spawnDirector == null)
        {
            spawnDirector = GetComponent<FishSpawnDirectorService>();
        }
    }

    private void OnDrawGizmos()
    {
        CacheReferences();
        if (targetCamera == null)
        {
            return;
        }

        float worldZ = transform.position.z;
        if (drawVisibleBounds)
        {
            Gizmos.color = Color.green;
            DrawRect(FishScreenBounds.GetWorldRect(targetCamera, 0f, worldZ));
        }

        if (drawPaddedBounds)
        {
            Gizmos.color = Color.cyan;
            DrawRect(FishScreenBounds.GetWorldRect(
                targetCamera,
                Mathf.Max(FishScreenBounds.MinimumViewportPadding, viewportPadding),
                worldZ
            ));
        }

        if (drawCenterArea)
        {
            Vector3 minimum = ViewportToWorld(
                0.5f - centerHalfWidth,
                0.5f - centerHalfHeight,
                worldZ
            );
            Vector3 maximum = ViewportToWorld(
                0.5f + centerHalfWidth,
                0.5f + centerHalfHeight,
                worldZ
            );
            Gizmos.color = Color.yellow;
            DrawRect(Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y));
        }

        if (drawSpawnAndExitPoints && levelDirector != null)
        {
            DrawPoint(levelDirector.LeftPos, Color.magenta);
            DrawPoint(levelDirector.RightPos, Color.magenta);
            DrawPoint(levelDirector.TopPos, Color.magenta);
            DrawPoint(levelDirector.BottomPos, Color.magenta);
        }

        int activeCount = FishMotionAgent.ActiveAgentCount;
        for (int i = 0; i < activeCount; i++)
        {
            FishMotionAgent agent = FishMotionAgent.GetActiveAgent(i);
            if (agent == null || !agent.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (drawSpacingRadii)
            {
                Gizmos.color = agent.IsBossMode ? Color.red : Color.white;
                Gizmos.DrawWireSphere(agent.transform.position, agent.SpacingRadius);
            }

            if (!drawFishRoutes)
            {
                continue;
            }

            int routeCount = agent.CopyRoutePoints(routeBuffer);
            if (routeCount <= 0)
            {
                continue;
            }

            Gizmos.color = agent.IsBossMode ? Color.red : Color.blue;
            Vector3 previous = agent.transform.position;
            int start = Mathf.Clamp(agent.CurrentRoutePointIndex, 0, routeCount - 1);
            for (int routeIndex = start; routeIndex < routeCount; routeIndex++)
            {
                Vector3 point = routeBuffer[routeIndex];
                Gizmos.DrawLine(previous, point);
                Gizmos.DrawWireSphere(point, 0.09f);
                previous = point;
            }
        }
    }

    private void OnGUI()
    {
        if (!drawRuntimeOverlay || !Application.isPlaying)
        {
            return;
        }

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                wordWrap = false,
                padding = new RectOffset(10, 10, 8, 8)
            };
        }

        overlayText.Length = 0;
        overlayText.AppendLine("FISH ARCADE RUNTIME DEBUG");

        if (levelDirector != null)
        {
            overlayText.Append("Level: ")
                .Append(levelDirector.CurrentLevelNumber)
                .Append("  Phase: ")
                .AppendLine(levelDirector.CurrentPhaseDisplayName);
            overlayText.Append("Tide transition: ")
                .AppendLine(levelDirector.IsTideChanging ? "ACTIVE" : "idle");
            overlayText.Append("Bosses: ")
                .Append(levelDirector.ActiveTargetBossCount)
                .Append("  timeout: ")
                .Append(levelDirector.CurrentBossTimeoutRemaining.ToString("0.0"))
                .Append("s  grace: ")
                .Append(levelDirector.CurrentBossGraceRemaining.ToString("0.0"))
                .AppendLine("s");
            overlayText.Append("Pool: ")
                .Append(levelDirector.PooledFishInstanceCount)
                .Append(" total / ")
                .Append(levelDirector.InactivePooledFishCount)
                .AppendLine(" inactive");
        }

        if (spawnDirector != null)
        {
            spawnDirector.RefreshPopulationSnapshot();
            overlayText.Append("Active: ")
                .Append(spawnDirector.ActiveFishCount)
                .Append("  center: ")
                .Append(spawnDirector.CenterFishCount)
                .Append("  entering/exiting: ")
                .Append(spawnDirector.EnteringFishCount)
                .Append('/')
                .AppendLine(spawnDirector.ExitingFishCount.ToString());
            overlayText.Append("Sides L/R/T/B: ")
                .Append(spawnDirector.LeftSideFishCount).Append('/')
                .Append(spawnDirector.RightSideFishCount).Append('/')
                .Append(spawnDirector.TopSideFishCount).Append('/')
                .AppendLine(spawnDirector.BottomSideFishCount.ToString());
            overlayText.Append("FPS: ")
                .Append(spawnDirector.MeasuredFrameRate.ToString("0.0"))
                .Append("  spawn-rate x")
                .AppendLine(spawnDirector.SpawnRateMultiplier.ToString("0.00"));
        }

        float height = Mathf.Max(150f, overlayText.ToString().Split('\n').Length * 20f + 16f);
        GUI.Box(
            new Rect(overlayPosition.x, overlayPosition.y, overlayWidth, height),
            overlayText.ToString(),
            overlayStyle
        );
    }

    private Vector3 ViewportToWorld(float x, float y, float worldZ)
    {
        float depth = Mathf.Abs(worldZ - targetCamera.transform.position.z);
        Vector3 point = targetCamera.ViewportToWorldPoint(new Vector3(x, y, depth));
        point.z = worldZ;
        return point;
    }

    private static void DrawRect(Rect rect)
    {
        Vector3 bottomLeft = new Vector3(rect.xMin, rect.yMin, 0f);
        Vector3 bottomRight = new Vector3(rect.xMax, rect.yMin, 0f);
        Vector3 topRight = new Vector3(rect.xMax, rect.yMax, 0f);
        Vector3 topLeft = new Vector3(rect.xMin, rect.yMax, 0f);
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }

    private static void DrawPoint(Transform point, Color color)
    {
        if (point == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(point.position, 0.20f);
        Gizmos.DrawLine(point.position + Vector3.left * 0.28f, point.position + Vector3.right * 0.28f);
        Gizmos.DrawLine(point.position + Vector3.down * 0.28f, point.position + Vector3.up * 0.28f);
    }
}
