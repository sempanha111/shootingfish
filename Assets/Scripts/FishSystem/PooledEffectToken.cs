using UnityEngine;
using UnityEngine.Rendering;

public sealed class PooledEffectToken : MonoBehaviour
{
    [System.NonSerialized] public int playVersion;
    [System.NonSerialized] public int poolIndex;

    [System.NonSerialized] public bool hasDefaultTransform;
    [System.NonSerialized] public Vector3 defaultLocalScale = Vector3.one;
    [System.NonSerialized] public Quaternion defaultLocalRotation = Quaternion.identity;

    [System.NonSerialized] public bool hasDefaultSorting;
    [System.NonSerialized] public int[] defaultSpriteSortingOrders;
    [System.NonSerialized] public int[] defaultRendererSortingLayerIds;
    [System.NonSerialized] public int[] defaultRendererSortingOrders;
    [System.NonSerialized] public int[] defaultSortingGroupLayerIds;
    [System.NonSerialized] public int[] defaultSortingGroupOrders;
    [System.NonSerialized] public bool[] defaultCanvasOverrideSorting;
    [System.NonSerialized] public int[] defaultCanvasSortingLayerIds;
    [System.NonSerialized] public int[] defaultCanvasSortingOrders;

    public void CaptureDefaultTransform(Transform target)
    {
        if (hasDefaultTransform || target == null)
        {
            return;
        }

        defaultLocalScale = target.localScale;
        defaultLocalRotation = target.localRotation;
        hasDefaultTransform = true;
    }

    public void RestoreDefaultTransform(Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (!hasDefaultTransform)
        {
            CaptureDefaultTransform(target);
        }

        target.localScale = defaultLocalScale;
        target.localRotation = defaultLocalRotation;
    }

    public void CaptureDefaultSorting(GameObject root)
    {
        if (hasDefaultSorting || root == null)
        {
            return;
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        defaultRendererSortingLayerIds = new int[renderers.Length];
        defaultRendererSortingOrders = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            defaultRendererSortingLayerIds[i] =
                renderers[i].sortingLayerID;
            defaultRendererSortingOrders[i] =
                renderers[i].sortingOrder;
        }

        SortingGroup[] groups =
            root.GetComponentsInChildren<SortingGroup>(true);
        defaultSortingGroupLayerIds = new int[groups.Length];
        defaultSortingGroupOrders = new int[groups.Length];

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] == null)
            {
                continue;
            }

            defaultSortingGroupLayerIds[i] = groups[i].sortingLayerID;
            defaultSortingGroupOrders[i] = groups[i].sortingOrder;
        }

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        defaultCanvasOverrideSorting = new bool[canvases.Length];
        defaultCanvasSortingLayerIds = new int[canvases.Length];
        defaultCanvasSortingOrders = new int[canvases.Length];

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null)
            {
                continue;
            }

            defaultCanvasOverrideSorting[i] =
                canvases[i].overrideSorting;
            defaultCanvasSortingLayerIds[i] =
                canvases[i].sortingLayerID;
            defaultCanvasSortingOrders[i] =
                canvases[i].sortingOrder;
        }

        hasDefaultSorting = true;
    }

    public void RestoreDefaultSorting(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        if (!hasDefaultSorting)
        {
            CaptureDefaultSorting(root);
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);
        int rendererCount = defaultRendererSortingLayerIds != null &&
            defaultRendererSortingOrders != null
            ? Mathf.Min(
                renderers.Length,
                Mathf.Min(
                    defaultRendererSortingLayerIds.Length,
                    defaultRendererSortingOrders.Length
                )
            )
            : 0;

        for (int i = 0; i < rendererCount; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].sortingLayerID =
                defaultRendererSortingLayerIds[i];
            renderers[i].sortingOrder =
                defaultRendererSortingOrders[i];
        }

        SortingGroup[] groups =
            root.GetComponentsInChildren<SortingGroup>(true);
        int groupCount = defaultSortingGroupLayerIds != null &&
            defaultSortingGroupOrders != null
            ? Mathf.Min(
                groups.Length,
                Mathf.Min(
                    defaultSortingGroupLayerIds.Length,
                    defaultSortingGroupOrders.Length
                )
            )
            : 0;

        for (int i = 0; i < groupCount; i++)
        {
            if (groups[i] == null)
            {
                continue;
            }

            groups[i].sortingLayerID = defaultSortingGroupLayerIds[i];
            groups[i].sortingOrder = defaultSortingGroupOrders[i];
        }

        Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
        int canvasCount = defaultCanvasSortingOrders != null &&
            defaultCanvasOverrideSorting != null
            ? Mathf.Min(
                canvases.Length,
                Mathf.Min(
                    defaultCanvasSortingOrders.Length,
                    defaultCanvasOverrideSorting.Length
                )
            )
            : 0;

        for (int i = 0; i < canvasCount; i++)
        {
            if (canvases[i] == null)
            {
                continue;
            }

            canvases[i].overrideSorting =
                defaultCanvasOverrideSorting[i];
            canvases[i].sortingOrder =
                defaultCanvasSortingOrders[i];
        }
    }
}
