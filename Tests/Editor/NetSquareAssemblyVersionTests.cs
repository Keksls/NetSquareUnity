using NetSquare.Core;
using NUnit.Framework;
using System;

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

        /// <summary>
        /// Confirms the stable Unity connection and dispatcher facade remains available.
        /// </summary>
        [Test]
        public void UnityFacadeExposesConnectionAndDispatcherSurface()
        {
            Assert.That(
                typeof(NSClient).GetProperty(nameof(NSClient.IsConnecting)),
                Is.Not.Null);
            Assert.That(
                typeof(NSClient).GetEvent(nameof(NSClient.OnConnectionAttemptCompleted)),
                Is.Not.Null);
            Assert.That(
                typeof(NSClient).GetMethod(
                    nameof(NSClient.CancelConnectionAttempt),
                    Type.EmptyTypes),
                Is.Not.Null);
            Assert.That(
                typeof(NSClient).GetMethod(
                    nameof(NSClient.AddAction),
                    new[] { typeof(Enum), typeof(NetSquareAction) }),
                Is.Not.Null);
            Assert.That(
                typeof(NSClient).GetMethod(
                    nameof(NSClient.SendMessage),
                    new[] { typeof(Enum), typeof(NetSquareAction) }),
                Is.Not.Null);
            Assert.That(
                typeof(NetSquareController).GetMethod(
                    nameof(NetSquareController.ConnectClient),
                    Type.EmptyTypes),
                Is.Not.Null);
        }
    }
}
