namespace Blink1.Api.IntegrationTest;

/// <summary>Every live test shares one device, so they run one at a time against one fixture.</summary>
[CollectionDefinition]
public sealed class LiveDevice : ICollectionFixture<Blink1Fixture>;
