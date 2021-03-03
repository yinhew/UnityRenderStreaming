using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.RenderStreaming.EditorTest
{
    class SampleTest
    {
        [UnityTest, Timeout(5000)]
        public IEnumerator Import()
        {
            var samples = Sample.FindByPackage("com.unity.renderstreaming", "3.0.0-preview.1");
            foreach (var sample in samples)
            {
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
            }

            bool completed = false;
            CompilationPipeline.assemblyCompilationFinished += (s, messages) =>
            {
                var messageTypes= messages.Select(m => m.type);
                Assert.That(messageTypes, Has.None.EqualTo(CompilerMessageType.Error));
            };
            CompilationPipeline.compilationFinished += o => { completed = true; };
            CompilationPipeline.RequestScriptCompilation();

            yield return new WaitUntil(() => completed);
        }
    }
}
