using AidynRetold.Plugins.Assets;
using Godot;

namespace AidynRetold.Views;

/// <summary>
/// Where what a body holds hangs on its model (<c>Bodies.Render</c>, <c>0x80013DCC</c>, through <c>SceneGetLocatorMtx</c>): a locator's
/// attachment node, turned by the locator model's rotation, of the model's own, not of a scene hung from another locator, as Alaron's
/// head is. The game's bodies and the editor's model page hang held items by it.
/// </summary>
public static class Attachments
{
    /// <summary>
    /// The node what's held at <paramref name="locator"/> hangs from, or null if the model has no such locator, as the original's
    /// <c>Scene.HasLocator</c> skips it. A plugin from before importer 51 has only the locator's node, which leaves out the locator
    /// model's rotation.
    /// </summary>
    public static Node3D? Find(Node3D model, int locator)
    {
        var nodes = Nodes.FindAll<Node3D>(model);
        return Own(SceneNames.Attachment(locator)) ?? Own(SceneNames.Locator(locator));

        Node3D? Own(string name) => nodes.FirstOrDefault(n => n.Name == name && LocatorsAbove(n, model) <= 1);
    }

    /// <summary>How many locators <paramref name="node"/> is or is under, up to <paramref name="root"/>: more than one is in a scene hung from another.</summary>
    private static int LocatorsAbove(Node node, Node root)
    {
        var count = 0;
        for (var n = node; n is not null && n != root; n = n.GetParent())
        {
            if (SceneNames.TryLocator(n.Name.ToString(), out _))
                count++;
        }
        return count;
    }
}
