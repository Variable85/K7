using AwesomeAssertions;
using K7.Shared;

namespace K7.Server.Application.UnitTests.Features.MusicSessions;

[TestFixture]
public class MusicSessionWindowTests
{
    [Test]
    public void Slice_ShouldKeepOnePreviousAndEightUpcoming_WhenSourceExists()
    {
        var items = Enumerable.Range(0, 30).ToList();

        var slice = MusicSessionWindow.Slice(items, currentIndex: 10, hasSource: true);

        slice.Items.Should().Equal(Enumerable.Range(9, 10));
        slice.CurrentIndex.Should().Be(1);
        slice.Items.Count.Should().Be(1 + 1 + MusicSessionWindow.SourcedUpcomingCap);
    }

    [Test]
    public void Slice_ShouldKeepCurrentThenForwardThenPrevious_WhenAdHocExceedsCap()
    {
        var items = Enumerable.Range(0, 150).ToList();

        var slice = MusicSessionWindow.Slice(items, currentIndex: 120, hasSource: false);

        slice.Items.Should().HaveCount(MusicSessionWindow.AdHocCap);
        slice.CurrentIndex.Should().Be(70);
        slice.Items[slice.CurrentIndex].Should().Be(120);
        slice.Items.Skip(slice.CurrentIndex).Should().Equal(Enumerable.Range(120, 30));
        slice.Items.Take(slice.CurrentIndex).Should().Equal(Enumerable.Range(50, 70));
    }

    [Test]
    public void Slice_ShouldKeepTheWholeQueue_WhenAdHocIsUnderCap()
    {
        var items = Enumerable.Range(0, 12).ToList();

        var slice = MusicSessionWindow.Slice(items, currentIndex: 3, hasSource: false);

        slice.Items.Should().Equal(items);
        slice.CurrentIndex.Should().Be(3);
    }
}
