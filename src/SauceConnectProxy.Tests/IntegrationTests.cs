using System;
using FluentAssertions;
using Xunit;

namespace SauceConnectProxy.Tests
{
    public class IntegrationTests
    {
        private const string TestUsername = "Test Account";
        private const string FailedToInitiateConnectionText = "Goodbye.";
        private const string SuccessfullConnectionText = "you may start your tests.";

        [Fact]
        public void CanConnectToSauceLabs()
        {
            using (var sauceProxy = new SauceConnectProxy("", "", 1234))
            {
                sauceProxy.Start();
                sauceProxy.Output.Should().EndWith(SuccessfullConnectionText);
            }
        }

        [Fact]
        public void TimeoutWillStopExecution()
        {
            using (var sauceProxy = new SauceConnectProxy(TestUsername, Guid.Empty.ToString(), 1234, TimeSpan.FromSeconds(1)))
            {
                Action action = () => sauceProxy.Start();
                action.Should().ThrowExactly<OperationCanceledException>();
            }
        }

        [Fact]
        public void TerminatesApplicationWhenInvalidDataIsProvided()
        {
            using (var sauceProxy = new SauceConnectProxy(TestUsername, Guid.Empty.ToString(), 1234))
            {
                Action action = () => sauceProxy.Start();
                action.Should().Throw<SauceConnectProxyException>().And.Message.Should().Contain("Starting up").And.EndWith(FailedToInitiateConnectionText);
                sauceProxy.Output.Should().EndWith(FailedToInitiateConnectionText);
            }
        }
    }
}
