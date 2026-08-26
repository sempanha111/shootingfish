using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple Home-scene Policy modal controller.
///
/// Recommended setup:
/// - Attach this script to the active HomeScreenController object.
/// - Keep PolicyModal under the Home Canvas.
/// - PolicyModal contains your finished policy image and a top-right Close button.
/// - PolicyModal starts inactive.
/// - Opening/closing only uses SetActive(true/false); nothing is instantiated.
/// </summary>
public class HomePolicyModal : MonoBehaviour
{
    [Header("Policy Modal")]
    [Tooltip("Drag the PolicyModal root GameObject here.")]
    [SerializeField] private GameObject policyModal;

    [Header("Buttons (Optional Auto Wiring)")]
    [Tooltip("Drag the Home scene Policy button here. If assigned, this script wires it automatically.")]
    [SerializeField] private Button policyButton;

    [Tooltip("Drag the top-right Close button from PolicyModal here. If assigned, this script wires it automatically.")]
    [SerializeField] private Button closeButton;

    [Header("Startup")]
    [Tooltip("Keep the Policy modal hidden when the Home scene starts.")]
    [SerializeField] private bool hideOnStart = true;

    private void Awake()
    {
        // The modal image already exists in the Home scene, so just hide it.
        if (policyModal != null && hideOnStart)
        {
            policyModal.SetActive(false);
        }

        // Optional: automatically connect the buttons when references are assigned.
        if (policyButton != null)
        {
            policyButton.onClick.RemoveListener(OpenPolicy);
            policyButton.onClick.AddListener(OpenPolicy);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePolicy);
            closeButton.onClick.AddListener(ClosePolicy);
        }
    }

    /// <summary>
    /// Called when the Home Policy button is clicked.
    /// </summary>
    public void OpenPolicy()
    {
        if (policyModal == null)
        {
            Debug.LogWarning("[HomePolicyModal] PolicyModal is not assigned in the Inspector.", this);
            return;
        }

        policyModal.SetActive(true);

        // Ensure it appears above the rest of the Home UI.
        policyModal.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Called by the top-right Close button.
    /// </summary>
    public void ClosePolicy()
    {
        if (policyModal == null)
        {
            return;
        }

        policyModal.SetActive(false);
    }

    /// <summary>
    /// Optional method if you ever want one button to toggle the modal.
    /// </summary>
    public void TogglePolicy()
    {
        if (policyModal == null)
        {
            return;
        }

        bool shouldOpen = !policyModal.activeSelf;
        policyModal.SetActive(shouldOpen);

        if (shouldOpen)
        {
            policyModal.transform.SetAsLastSibling();
        }
    }

    public bool IsPolicyOpen()
    {
        return policyModal != null && policyModal.activeSelf;
    }
}
