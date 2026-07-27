using NetSquare.Core;
using NUnit.Framework;
using UnityEngine;

namespace NetSquare.Client.Tests
{
    /// <summary>
    /// Verifies Unity settings conversion into NetSquare 1.0.17 configuration.
    /// </summary>
    public sealed class NetSquareSettingsTests
    {
        /// <summary>
        /// Confirms security and queue settings are copied without sharing mutable configurations.
        /// </summary>
        [Test]
        public void CreateClientConfigurationCopiesTransportSecurityAndBounds()
        {
            NetSquareSettings settings = ScriptableObject.CreateInstance<NetSquareSettings>();
            try
            {
                settings.Host = "game.example.com";
                settings.Port = 6000;
                settings.UseTLS = true;
                settings.TLSServerName = "game.example.com";
                settings.UseUdpAuthentication = true;
                settings.MaxQueuedInboundMessages = 1234;
                settings.MaxPendingReplyCallbacks = 4321;
                settings.ReplyCallbackTimeoutMilliseconds = 15000;
                settings.SynchronizationTransport = NetSquareSyncTransport.UnreliableUdp;

                NetSquareClientConfiguration first = settings.CreateClientConfiguration();
                NetSquareClientConfiguration second = settings.CreateClientConfiguration();

                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.Host, Is.EqualTo("game.example.com"));
                Assert.That(first.Port, Is.EqualTo(6000));
                Assert.That(first.UseTLS, Is.True);
                Assert.That(first.UseUdpAuthentication, Is.True);
                Assert.That(first.MaxQueuedInboundMessages, Is.EqualTo(1234));
                Assert.That(first.MaxPendingReplyCallbacks, Is.EqualTo(4321));
                Assert.That(first.ReplyCallbackTimeoutMilliseconds, Is.EqualTo(15000));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        /// <summary>
        /// Confirms an invalid UDP synchronization protocol is rejected before networking starts.
        /// </summary>
        [Test]
        public void ValidateRejectsUdpSynchronizationWithoutUdpProtocol()
        {
            NetSquareSettings settings = ScriptableObject.CreateInstance<NetSquareSettings>();
            try
            {
                settings.ProtocoleType = NetSquareProtocoleType.TCP;
                settings.SynchronizationTransport = NetSquareSyncTransport.UnreliableUdp;
                Assert.Throws<System.InvalidOperationException>(() => settings.Validate());
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
