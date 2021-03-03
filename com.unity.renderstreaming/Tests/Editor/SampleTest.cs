using NUnit.Framework;
using UnityEditor.PackageManager.UI;

namespace Unity.RenderStreaming.EditorTest
{
    class SampleTest
    {
        [Test]
        public void Import()
        {
            var samples = Sample.FindByPackage("com.unity.renderstreaming", "3.0.0-preview.1");
            foreach (var sample in samples)
            {
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
            }
        }
    }
}
