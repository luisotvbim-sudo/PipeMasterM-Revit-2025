namespace PipeMasterMEP;

internal static class TestMode
{
#if PIPEMASTER_TEST_MODE
	public static bool Enabled { get; } = true;
#else
    public static bool Enabled { get; } = false;
#endif
}
