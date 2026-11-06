using System.Collections.Generic;
using UnityEngine;

public class LayerBasedSwitch : Switch
{
    [SerializeField] private LayerMask activationLayer;
    private readonly HashSet<Collider2D> contacts = new HashSet<Collider2D>();

    public virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (!isServer) return;
        if (other == null || !IsLayerMatched(other.gameObject) || !contacts.Add(other)) return;
        SetSwitchState(true);
    }

    public virtual void OnTriggerExit2D(Collider2D other)
    {
        if (!isServer) return;
        if (!contacts.Remove(other)) return;
        SetSwitchState(contacts.Count > 0 || HasAdditionalContact);
    }

    protected virtual bool HasAdditionalContact => false;

    protected virtual void FixedUpdate()
    {
        if (!isServer) return;
        contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy || !IsLayerMatched(c.gameObject));
        SetSwitchState(contacts.Count > 0 || HasAdditionalContact);
    }

    private void OnDisable()
    {
        contacts.Clear();
        if (Mirror.NetworkServer.active && isServer) SetSwitchState(false);
    }

    private bool IsLayerMatched(GameObject obj) => (activationLayer.value & (1 << obj.layer)) != 0;
}
