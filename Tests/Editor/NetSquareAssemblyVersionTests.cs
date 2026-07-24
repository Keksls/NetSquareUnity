using NUnit.Framework;

namespace NetSquare.Client.Editor.Tests
{
    /// <summary>
    /// Verifies the exact Client/Core version contract required by Handshake V2.
    /// </summary>
    public sealed class NetSquareAssemblyVersionTests
    {
        /// <summary>
        /// Confirms both embedded assemblies match the package version contract.
        /// </summary>
        [Test]
        public void EmbeddedAssembliesMatchRequiredVersion()
        {
            string clientVersion =
                typeof(NetSquareClient).Assembly.GetName().Version.ToString();
            string coreVersion =
                typeof(NetSquare.Core.NetworkMessage).Assembly.GetName().Version.ToString();

            Assert.That(
                clientVersion,
                Is.EqualTo(NetSquarePackageInfo.RequiredAssemblyVersion));
            Assert.That(
                coreVersion,
                Is.EqualTo(NetSquarePackageInfo.RequiredAssemblyVersion));
        }
    }
}
