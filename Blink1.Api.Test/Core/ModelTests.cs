using Blink1.Api.Test.Support;

namespace Blink1.Api.Test.Core;

public class ModelTests
{
	[Theory]
	[InlineData("#FF8000")]
	[InlineData("ff8000")]
	[InlineData("0xFf8000")]
	[InlineData("0XFF8000")]
	[InlineData("  #ff8000 ")]
	public void Color_Parse_AcceptsTheHexForms(string text)
		=> Blink1Color.Parse(text).Should().Be(new Blink1Color(255, 128, 0));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("#12345")]
	[InlineData("#1234567")]
	[InlineData("zzzzzz")]
	[InlineData("12,34,56")]
	public void Color_TryParse_RejectsOtherText(string? text)
		=> Blink1Color.TryParse(text, out _).Should().BeFalse();

	[Fact]
	public void Color_Parse_ThrowsOnOtherText()
		=> FluentActions.Invoking(() => Blink1Color.Parse("red")).Should().Throw<FormatException>().WithMessage("'red' is not a colour*");

	[Fact]
	public void Color_ToString_IsHex()
		=> new Blink1Color(1, 0xAB, 255).ToString().Should().Be("#01ABFF");

	[Fact]
	public void Colors_AreTheNamedColours()
		=> new[] { Blink1Colors.Black, Blink1Colors.White, Blink1Colors.Red, Blink1Colors.Green, Blink1Colors.Blue, Blink1Colors.Yellow, Blink1Colors.Cyan, Blink1Colors.Magenta }
			.Select(c => c.ToString())
			.Should().Equal("#000000", "#FFFFFF", "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#00FFFF", "#FF00FF");

	[Theory]
	[InlineData("blink(1) mk2", "20002", Blink1Generation.Mk2)]
	[InlineData("blink(1) MK3", null, Blink1Generation.Mk3)]
	[InlineData("blink(1) mk4", "20000001", Blink1Generation.Mk4)]
	[InlineData("blink(1)", "00001234", Blink1Generation.Mk1)]
	[InlineData("blink(1)", "20000001", Blink1Generation.Mk2)]
	[InlineData("blink(1)", "3000ABCD", Blink1Generation.Mk3)]
	[InlineData(null, "40000000", Blink1Generation.Mk4)]
	[InlineData("blink(1)", "20002", Blink1Generation.Unknown)]
	[InlineData("blink(1)", "2000000G", Blink1Generation.Unknown)]
	[InlineData(null, null, Blink1Generation.Unknown)]
	public void DeviceInfo_GenerationComesFromTheProductThenTheSerial(string? product, string? serial, Blink1Generation expected)
		=> Blink1DeviceInfo.GenerationOf(product, serial).Should().Be(expected);

	[Theory]
	[InlineData(Blink1Generation.Unknown, 0)]
	[InlineData(Blink1Generation.Mk1, 12)]
	[InlineData(Blink1Generation.Mk2, 32)]
	[InlineData(Blink1Generation.Mk3, 32)]
	[InlineData(Blink1Generation.Mk4, 32)]
	public void DeviceInfo_PatternLineCount_FollowsTheGeneration(Blink1Generation generation, int expected)
		=> new Blink1DeviceInfo("p", "s", "n", "m", 0, generation).PatternLineCount.Should().Be(expected);

	[Fact]
	public void DeviceInfo_From_TurnsMissingStringsIntoEmptyOnes()
		=> Blink1DeviceInfo.From(TestDevices.Unknown).Should().Be(new Blink1DeviceInfo("path-unknown", "", "", "", 0, Blink1Generation.Unknown));

	[Fact]
	public void DeviceInfo_From_CopiesTheDescriptor()
		=> Blink1DeviceInfo.From(TestDevices.Mk2).Should().Be(new Blink1DeviceInfo("path-mk2", "20002", "blink(1) mk2", "ThingM", 2, Blink1Generation.Mk2));

	[Fact]
	public void Options_DefaultToGammaCorrectionAndReadWrite()
	{
		var options = new Blink1ClientOptions();

		options.ApplyGammaCorrection.Should().BeTrue();
		options.IsReadOnly.Should().BeFalse();
		options.SerialNumber.Should().BeNull();
		options.DevicePath.Should().BeNull();
	}

	[Fact]
	public void UsbIds_AreThingMs()
	{
		Blink1DeviceInfo.VendorId.Should().Be(0x27B8);
		Blink1DeviceInfo.ProductId.Should().Be(0x01ED);
	}
}
