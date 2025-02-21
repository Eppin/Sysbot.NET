using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Pokemon;
using Xunit;

namespace SysBot.Tests;

public class StopConditionsTests
{
    [Theory]
    [InlineData(TargetFlawlessIVsType.Disabled, true)]
    [InlineData(TargetFlawlessIVsType._0, true)]
    [InlineData(TargetFlawlessIVsType._1, true)]
    [InlineData(TargetFlawlessIVsType._2, true)]
    [InlineData(TargetFlawlessIVsType._3, true)]
    [InlineData(TargetFlawlessIVsType._4, true)]
    [InlineData(TargetFlawlessIVsType._5, false)]
    [InlineData(TargetFlawlessIVsType._6, false)]
    public async Task TestEncounterFound_Scorbunny_FlawlessIVs(TargetFlawlessIVsType targetFlawlessIVs, bool expected)
    {
        // Arrange
        var bytes = await GetResource("0813 - Scorbunny - 4F320450C78B.pk9");

        var pk9 = new PK9(bytes);
        var sc = new StopConditionSettings { SearchConditions = [new() { FlawlessIVs = targetFlawlessIVs }] };

        // Act
        var result = StopConditionSettings.EncounterFound(pk9, sc, null);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("x/x/x/x/x/x", "x/x/x/x/x/x", true)]
    [InlineData("x/31/31/31/x/31", "x/31/31/31/x/31", true)]
    [InlineData("31/x/x/x/x/x", "31/x/x/x/x/x", false)]
    public async Task TestEncounterFound_Scorbunny_MatchIVs(string targetMinIVs, string targetMaxIVs, bool expected)
    {
        // Arrange
        var bytes = await GetResource("0813 - Scorbunny - 4F320450C78B.pk9");

        var pk9 = new PK9(bytes);
        var sc = new StopConditionSettings { SearchConditions = [new() { TargetMinIVs = targetMinIVs, TargetMaxIVs = targetMaxIVs, FlawlessIVs = TargetFlawlessIVsType.Disabled }] };

        // Act
        var result = StopConditionSettings.EncounterFound(pk9, sc, null);

        // Assert
        Assert.Equal(expected, result);
    }

    private static async Task<byte[]> GetResource(string file)
    {
        var info = Assembly.GetExecutingAssembly().GetName();

        await using var stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream($"{info.Name}.Resources.{file}")!;

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
