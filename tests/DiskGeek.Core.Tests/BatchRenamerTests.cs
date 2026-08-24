using DiskGeek.Core.Renaming;
using Xunit;

namespace DiskGeek.Core.Tests;

public class BatchRenamerTests
{
    private static string Dir => Path.Combine(Path.GetTempPath(), "diskgeek-tests-notreal");
    private static string P(string name) => Path.Combine(Dir, name);

    private static IReadOnlyList<string> Paths(params string[] names) =>
        names.Select(P).ToList();

    [Fact]
    public void PrefixAndSuffixWrapTheNameButLeaveTheExtensionAlone()
    {
        var preview = BatchRenamer.Preview(Paths("holiday.jpg"),
            new RenameOptions { Prefix = "2026-", Suffix = "-edited" });

        Assert.Equal("2026-holiday-edited.jpg", preview[0].NewName);
        Assert.Null(preview[0].Error);
    }

    [Fact]
    public void FindAndReplaceRunsAgainstTheNameWithoutItsExtension()
    {
        var preview = BatchRenamer.Preview(Paths("DSC_0001.jpg"),
            new RenameOptions { FindText = "DSC_", ReplaceText = "Photo " });

        Assert.Equal("Photo 0001.jpg", preview[0].NewName);
    }

    [Fact]
    public void ARegexFindIsHonouredWhenFindIsRegexIsSet()
    {
        var preview = BatchRenamer.Preview(Paths("report-2024-final.pdf"),
            new RenameOptions { FindText = @"\d{4}", ReplaceText = "YYYY", FindIsRegex = true });

        Assert.Equal("report-YYYY-final.pdf", preview[0].NewName);
    }

    [Fact]
    public void TheSameTextIsNotTreatedAsARegexUnlessAsked()
    {
        // "." is a regex wildcard but must be literal here, so nothing should match.
        var preview = BatchRenamer.Preview(Paths("abc.txt"),
            new RenameOptions { FindText = ".", ReplaceText = "!" });

        Assert.Equal("abc.txt", preview[0].NewName);
    }

    [Fact]
    public void TheCounterIncrementsAcrossTheBatchAndIsZeroPadded()
    {
        var preview = BatchRenamer.Preview(Paths("a.txt", "b.txt", "c.txt"),
            new RenameOptions { UseCounter = true });

        // Default CounterStart is 1 and CounterDigits is 2, and the number is appended directly to
        // the name - there is no separator, despite what RenameOptions' own doc comment suggests.
        Assert.Equal(new[] { "a01.txt", "b02.txt", "c03.txt" },
            preview.Select(p => p.NewName).ToArray());
    }

    [Fact]
    public void TheCounterHonoursStartStepAndDigits()
    {
        var preview = BatchRenamer.Preview(Paths("a.txt", "b.txt"),
            new RenameOptions { UseCounter = true, CounterStart = 5, CounterStep = 10, CounterDigits = 4 });

        Assert.Equal(new[] { "a0005.txt", "b0015.txt" },
            preview.Select(p => p.NewName).ToArray());
    }

    [Fact]
    public void AnEntryThatWouldEndUpWithAnEmptyNameIsFlaggedRatherThanThrowing()
    {
        var preview = BatchRenamer.Preview(Paths("abc.txt"),
            new RenameOptions { FindText = "abc", ReplaceText = "" });

        Assert.NotNull(preview[0].Error);
        Assert.Contains("empty", preview[0].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TwoEntriesCollapsingToTheSameNameAreFlagged()
    {
        // Stripping the digits makes both files want to be "file.txt".
        var preview = BatchRenamer.Preview(Paths("file1.txt", "file2.txt"),
            new RenameOptions { FindText = @"\d", ReplaceText = "", FindIsRegex = true });

        Assert.Equal("file.txt", preview[0].NewName);
        Assert.Null(preview[0].Error);

        Assert.NotNull(preview[1].Error);
        Assert.Contains("collides", preview[1].Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OneBadEntryDoesNotStopTheRestOfThePreviewBeingProduced()
    {
        var preview = BatchRenamer.Preview(Paths("abc.txt", "keep.txt"),
            new RenameOptions { FindText = "abc", ReplaceText = "" });

        Assert.Equal(2, preview.Count);
        Assert.NotNull(preview[0].Error);
        Assert.Null(preview[1].Error);
    }

    [Fact]
    public void AnEmptyOptionsSetIsRecognisedAsANoOp() =>
        Assert.True(new RenameOptions().IsEmpty);

    [Fact]
    public void OptionsWithAnythingSetAreNotEmpty()
    {
        Assert.False(new RenameOptions { Prefix = "x" }.IsEmpty);
        Assert.False(new RenameOptions { UseCounter = true }.IsEmpty);
        Assert.False(new RenameOptions { FindText = "a" }.IsEmpty);
    }

    [Fact]
    public void PreviewRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => BatchRenamer.Preview(null!, new RenameOptions()));
        Assert.Throws<ArgumentNullException>(() => BatchRenamer.Preview(Paths("a.txt"), null!));
    }

    [Fact]
    public void ApplySkipsEntriesThatAlreadyHaveAnErrorInsteadOfTouchingDisk()
    {
        var entry = new RenamePreviewEntry(P("nope.txt"), P("also-nope.txt"), "Resulting name would be empty.");

        var result = BatchRenamer.Apply(new[] { entry });

        Assert.False(result.AllSucceeded);
        Assert.Empty(result.Renamed);
        Assert.Single(result.Failed);
    }

    [Fact]
    public void ApplyTreatsAnUnchangedNameAsNothingToDo()
    {
        var same = P("unchanged.txt");
        var result = BatchRenamer.Apply(new[] { new RenamePreviewEntry(same, same, null) });

        // Neither renamed nor failed - it is simply skipped, so no File.Move is attempted.
        Assert.Empty(result.Renamed);
        Assert.Empty(result.Failed);
        Assert.True(result.AllSucceeded);
    }
}
