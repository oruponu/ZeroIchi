namespace ZeroIchi.Tests.FileTypes;

// Regex matches time out on wall-clock time, so tests that expect quick matches
// must not compete for the CPU with other tests
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RegexTimeoutCollection
{
    public const string Name = "RegexTimeout";
}
