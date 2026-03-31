using System.Collections.Generic;
using UnityEngine;

public static class InteractableRegistry
{
    public struct Entry
    {
        public IInteractable Interactable;
        public Collider Collider;
        public Transform Transform;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    public static IReadOnlyList<Entry> Entries => entries;

    public static void Register(IInteractable interactable, Collider collider, Transform transform)
    {
        if (interactable == null || collider == null || transform == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i].Interactable, interactable))
                return;
        }

        entries.Add(new Entry
        {
            Interactable = interactable,
            Collider = collider,
            Transform = transform
        });
    }

    public static void Unregister(IInteractable interactable)
    {
        if (interactable == null)
            return;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(entries[i].Interactable, interactable))
                entries.RemoveAt(i);
        }
    }
}
