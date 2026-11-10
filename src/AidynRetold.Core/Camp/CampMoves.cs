using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Camp;

/// <summary>
/// The poses a figure drawn from a model passes through on its way from one pose to another, the user's (Nov 9, 2026), so its legs don't pass through one another and it
/// doesn't simply melt between standing and sitting. Between two sits it goes through the first of its sits, knees drawn up. Sitting down from standing it goes through its
/// <see cref="CampFigureModel.SitDown"/> poses to that first sit, bending its knees and lowering itself, a hand going down behind it; standing up it goes back the same way.
/// </summary>
public static class CampMoves
{
    /// <param name="from">The pose it's in, or moving to.</param>
    /// <returns>The poses in order, the last of them <paramref name="to"/>.</returns>
    public static IReadOnlyList<CampModelPose> Path(CampFigureModel model, CampModelPose? from, bool fromSeated, CampModelPose to, bool toSeated)
    {
        if (model.Sits.Count == 0 || !fromSeated && !toSeated)
            return [to];
        var through = model.Sits[0];
        var down = model.SitDown ?? [];
        List<CampModelPose> path = [];
        if (fromSeated && toSeated)
        {
            if (from != through && to != through)
                path.Add(through);
        }
        else if (toSeated)
        {
            path.AddRange(down);
            if (to != through)
                path.Add(through);
        }
        else
        {
            if (from != through)
                path.Add(through);
            path.AddRange(down.Reverse());
        }
        path.Add(to);
        return path;
    }
}
