using NUnit.Framework;
using UnityEditor;

namespace OrbitSort.Tests.EditMode
{
    public sealed class ProjectConfigurationTests
    {
        [Test]
        public void MobileOrientationIsUprightPortrait()
        {
            Assert.That(
                PlayerSettings.defaultInterfaceOrientation,
                Is.EqualTo(UIOrientation.Portrait));
            Assert.That(
                PlayerSettings.allowedAutorotateToPortrait,
                Is.True);
            Assert.That(
                PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                Is.False);
            Assert.That(
                PlayerSettings.allowedAutorotateToLandscapeLeft,
                Is.False);
            Assert.That(
                PlayerSettings.allowedAutorotateToLandscapeRight,
                Is.False);
        }
    }
}
