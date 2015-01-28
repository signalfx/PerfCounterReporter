using PerfCounterReporter.Interop;
using Xunit;

namespace PerfCounterReporter.UnitTest
{
    public class PdhPathHelperTest
    {

        [Fact]
        public void GetMoo()
        {
            PdhPathHandler helper = new PdhPathHandler();
            foreach (PdhCounterPathElement element in helper.GetPathElements(new string[] { "\\PhysicalDisk(*)\\Avg. Disk sec/Write", "\\Paging File(*)\\% Usage" }))
            {
                System.Console.WriteLine(string.Format("moo: {0}", element.ToString()));
            }
        }
    }
}
