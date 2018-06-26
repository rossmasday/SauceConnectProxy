using System;
using FluentAssertions;
using Xunit;

namespace SauceConnectProxy.Tests
{
    using System.Threading.Tasks;
    using OpenQA.Selenium.Chrome;
    using OpenQA.Selenium.Remote;

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
        public void TimeoutWillStopExecution()
        {
            using (var sauceProxy = new SauceConnectProxy(1234, TimeSpan.FromSeconds(5)))
            {
                Func<Task> action = async () => await sauceProxy.StartAsync().ConfigureAwait(false);
                action.Should().ThrowExactly<TaskCanceledException>();
            }
        }
    }
}
