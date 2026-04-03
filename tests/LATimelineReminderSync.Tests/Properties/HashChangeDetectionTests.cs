using FsCheck;
using FsCheck.Xunit;
using LATimelineReminderSync;

namespace LATimelineReminderSync.Tests.Properties;

/// <summary>
/// Feature: wow-timeline-reminders-sync, Property 1: Hash-based change detection is consistent
/// **Validates: Requirements 1.4, 1.5**
/// </summary>
public class HashChangeDetectionTests
{
    [Property(MaxTest = 100)]
    public Property SameString_ProducesSameHash()
    {
        return Prop.ForAll(Arb.From<NonEmptyString>(), s =>
        {
            var hash1 = ContentHashStore.ComputeHash(s.Get);
            var hash2 = ContentHashStore.ComputeHash(s.Get);
            return (hash1 == hash2).Label("Same string must produce same hash (idempotent)");
        });
    }

    [Property(MaxTest = 100)]
    public Property DifferentStrings_ProduceDifferentHashes()
    {
        var gen = from a in Arb.Generate<NonEmptyString>()
                  from b in Arb.Generate<NonEmptyString>()
                  where a.Get != b.Get
                  select (a.Get, b.Get);

        return Prop.ForAll(gen.ToArbitrary(), pair =>
        {
            var hash1 = ContentHashStore.ComputeHash(pair.Item1);
            var hash2 = ContentHashStore.ComputeHash(pair.Item2);
            return (hash1 != hash2).Label("Different strings should produce different hashes");
        });
    }

    [Property(MaxTest = 100)]
    public Property HashStore_RoundTrip_ReturnsSameHash()
    {
        return Prop.ForAll(Arb.From<NonEmptyString>(), s =>
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"hashtest_{Guid.NewGuid()}.hash");
            try
            {
                var store = new ContentHashStore(tempFile);
                var hash = ContentHashStore.ComputeHash(s.Get);

                store.SetLastHash(hash);
                var retrieved = store.GetLastHash();

                return (hash == retrieved).Label("Set then Get should return same hash");
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        });
    }
}
