using Ecare.Application.Services;

namespace Ecare.Application.Tests;

public class QueueOrderingTests
{
    private static QueueSnapshot.LegendRow Row(
        int id, DateTime addedToQueueAt, bool pinned = false, DateTime? pinedAt = null)
        => new()
        {
            Id = id,
            AddedToQueueAt = addedToQueueAt,
            IsPined = pinned,
            PinedAt = pinedAt,
        };

    [Fact]
    public void Orders_fifo_by_added_to_queue()
    {
        var later = Row(1, new DateTime(2026, 6, 22, 10, 0, 0));
        var earlier = Row(2, new DateTime(2026, 6, 22, 9, 0, 0));

        var ordered = QueueSnapshot.OrderForQueue(new[] { later, earlier }).ToList();

        Assert.Equal(new[] { 2, 1 }, ordered.Select(r => r.Id));
    }

    [Fact]
    public void Unset_timestamp_sorts_last_not_first()
    {
        // Regression: a just-scanned PAL/SAC row with no queue timestamp must NOT jump to the top.
        var scanned = Row(1, new DateTime(2026, 6, 22, 9, 0, 0));
        var unset = Row(2, default); // AddedToQueueAt = 0001-01-01, ParkingAt default, FirstPlaceAt null

        var ordered = QueueSnapshot.OrderForQueue(new[] { unset, scanned }).ToList();

        Assert.Equal(new[] { 1, 2 }, ordered.Select(r => r.Id));
    }

    [Fact]
    public void Pinned_sorts_before_unpinned_even_if_scanned_later()
    {
        var unpinnedEarly = Row(1, new DateTime(2026, 6, 22, 9, 0, 0));
        var pinnedLate = Row(2, new DateTime(2026, 6, 22, 11, 0, 0),
            pinned: true, pinedAt: new DateTime(2026, 6, 22, 11, 5, 0));

        var ordered = QueueSnapshot.OrderForQueue(new[] { unpinnedEarly, pinnedLate }).ToList();

        Assert.Equal(new[] { 2, 1 }, ordered.Select(r => r.Id));
    }

    [Fact]
    public void Earliest_pin_sorts_first_among_pinned()
    {
        var pinnedLater = Row(1, new DateTime(2026, 6, 22, 9, 0, 0),
            pinned: true, pinedAt: new DateTime(2026, 6, 22, 10, 0, 0));
        var pinnedEarlier = Row(2, new DateTime(2026, 6, 22, 9, 0, 0),
            pinned: true, pinedAt: new DateTime(2026, 6, 22, 9, 30, 0));

        var ordered = QueueSnapshot.OrderForQueue(new[] { pinnedLater, pinnedEarlier }).ToList();

        Assert.Equal(new[] { 2, 1 }, ordered.Select(r => r.Id));
    }
}
