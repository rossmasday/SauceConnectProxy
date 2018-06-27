namespace SauceConnectProxy.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using OpenQA.Selenium.Chrome;
    using OpenQA.Selenium.Remote;
    using Xunit;

    public class IntegrationTests
    {
        private const string SuccessfullConnectionText = "you may start your tests.";

        [Fact]
        public async Task CanConnectToSauceLabs()
        {
            using (var sauceProxy = new SauceConnectProxy(1234))
            {
                await sauceProxy.StartAsync();

                var options = (DesiredCapabilities) new ChromeOptions().ToCapabilities();
                options.SetCapability("username", sauceProxy.Username);
                options.SetCapability("accesskey", sauceProxy.AccessKey);
                options.SetCapability("name", "SauceConnectProxyCanConnectToSauceLabs");
                using (var driver = new RemoteWebDriver(sauceProxy.ProxyAddress, options))
                {
                    driver.Navigate().GoToUrl(new Uri("http://example.com/"));
                    driver.Title.Should().Be("Example Domain");
                }

                sauceProxy.Output.Should().EndWith(SuccessfullConnectionText);
            }
        }

        [Fact]
        public async Task CanConnectUseExistingConnection()
        {
            using (var sauceProxy1 = new SauceConnectProxy(1234))
            {
                await sauceProxy1.StartAsync();
                sauceProxy1.Output.Should().EndWith(SuccessfullConnectionText);
                using (var sauceProxy2 = new SauceConnectProxy(4445))
                {
                    await sauceProxy2.StartAsync();
                    sauceProxy2.ProxyAddress.Should().Be(sauceProxy1.ProxyAddress);
                    sauceProxy2.Output.Should().BeNull();
                }
            }
        }

        [Fact]
        public void TimeoutWillStopExecution()
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(10000);
            using (var sauceProxy = new SauceConnectProxy(1234))
            {
                Func<Task> action = async () => await sauceProxy.StartAsync(tokenSource.Token).ConfigureAwait(false);
                action.Should().ThrowExactly<TaskCanceledException>();
            }
        }
    }
}
