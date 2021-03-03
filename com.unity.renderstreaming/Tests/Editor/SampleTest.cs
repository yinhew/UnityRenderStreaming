using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.RenderStreaming.EditorTest
{
    class SampleTest
    {
        /// <summary>
        /// see this manual.
        /// https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/reference-recompile-scripts.html
        /// </summary>
        /// <returns></returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var samples = Sample.FindByPackage("com.unity.renderstreaming", "3.0.0-preview.1");
            foreach (var sample in samples)
            {
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
            }
            AssetDatabase.Refresh();
            yield return new RecompileScripts();
        }


        [Test]
        public void Test()
        {
        }
    }
}
