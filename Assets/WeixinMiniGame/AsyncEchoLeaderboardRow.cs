using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class AsyncEchoLeaderboardRow : MonoBehaviour
{
    public Image surface, rankBadge;
    public Text rank, runner, distance, lead;

    public void Bind(AsyncEchoLeaderboardEntry item)
    {
        surface.color = item.isMe ? AsyncEchoPanelView.Selected : AsyncEchoPanelView.Raised;
        rankBadge.color = item.rank == 1 ? AsyncEchoPanelView.Echo : AsyncEchoPanelView.Surface;
        rank.color = item.rank == 1 ? AsyncEchoPanelView.Surface : AsyncEchoPanelView.Muted;
        rank.text = item.rank.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
        runner.supportRichText = false;
        runner.color = AsyncEchoPanelView.Foreground;
        runner.text = item.displayLabel + (item.isMe ? "（我）" : "");
        distance.color = item.isMe ? AsyncEchoPanelView.Echo : AsyncEchoPanelView.Foreground;
        distance.text = item.distanceMeters.ToString("0.0", CultureInfo.InvariantCulture) + " 米";
        lead.color = AsyncEchoPanelView.Muted;
        lead.text = "领先 " + item.playerLeadMeters.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + " 米";
        var layout = GetComponent<LayoutElement>();
        if (layout != null) layout.minHeight = layout.preferredHeight = EchoRunAccessibility.LargeText ? 208f : 174f;
        EchoRunAccessibility.ApplyToHierarchy(transform);
    }
}
