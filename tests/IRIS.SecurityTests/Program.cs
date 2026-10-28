using System;
using System.Linq;
using System.Threading.Tasks;

namespace IRIS.SecurityTests;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            string[] results = System.Linq.Enumerable.Concat(await SecurityChecks.RunAsync(), await LocalInferenceChecks.RunAsync()).ToArray();
            if (OperatingSystem.IsWindows()) results = results.Concat(await MemoryChecks.RunAsync()).ToArray();
            foreach (string result in results) Console.WriteLine(result);
            Console.WriteLine($"{results.Length} contrôles réussis.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
