using Ecare.Application.Services;

namespace Ecare.Application.Tests;

public class QueueOrderingTests
{
    private static QueueSnapshot.LegendRow Row(
        int id, DateTime addedToQueueAt, bool pinned = false, DateTime? pinedAt = null,
        DateTime? dateAffectation = null, DateTime parkingAt = default)
        => new()
        {
            Id = id,
            AddedToQueueAt = addedToQueueAt,
            IsPined = pinned,
            PinedAt = pinedAt,
            DateAffectation = dateAffectation,
            ParkingAt = parkingAt,
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
    public void Affectation_time_beats_queue_and_parking_time()
    {
        // CFR affectée à 10h00 mais camion pointé tôt (file à 08h00) vs
        // EXW affectée à 09h00, camion pointé après (file à 10h30) :
        // l'ordre réel de traitement suit l'heure d'affectation → EXW d'abord.
        var cfrAffecteeTard = Row(1, new DateTime(2026, 9, 21, 8, 0, 0),
            dateAffectation: new DateTime(2026, 9, 21, 10, 0, 0));
        var exwAffecteeTot = Row(2, new DateTime(2026, 9, 21, 10, 30, 0),
            dateAffectation: new DateTime(2026, 9, 21, 9, 0, 0));

        var ordered = QueueSnapshot.OrderForQueue(new[] { cfrAffecteeTard, exwAffecteeTot }).ToList();

        Assert.Equal(new[] { 2, 1 }, ordered.Select(r => r.Id));
    }

    [Fact]
    public void Missing_affectation_falls_back_to_queue_then_parking_time()
    {
        // Ligne historique sans DateAffectation (sp pas encore migrée) : comportement inchangé.
        var sansAffectation = Row(1, new DateTime(2026, 9, 21, 9, 0, 0));
        var avecAffectation = Row(2, default,
            dateAffectation: new DateTime(2026, 9, 21, 9, 30, 0),
            parkingAt: new DateTime(2026, 9, 21, 9, 30, 0));

        var ordered = QueueSnapshot.OrderForQueue(new[] { avecAffectation, sansAffectation }).ToList();

        Assert.Equal(new[] { 1, 2 }, ordered.Select(r => r.Id));
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
