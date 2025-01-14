using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MicroBatchFramework.Tests
{
    public class CommandAttributeTest
    {
        class CommandAttributeTestCommand : BatchBase
        {
            ResultContainer _Result;
            public CommandAttributeTestCommand(ResultContainer r)
            {
                _Result = r;
            }
            [Command("test")]
            public void TestCommand(int value)
            {
                _Result.X = value;
            }
        }
        class ResultContainer
        {
            public int X;
        }
        [Fact]
        public async Task TestCommandName()
        {
            var host = BatchHost.CreateDefaultBuilder()
                .ConfigureServices((c, services) =>
                {
                    services.AddSingleton<ResultContainer>();
                })
                .UseBatchEngine<CommandAttributeTestCommand>(new string[]{ "test", "-value", "1" })
                .Build();
            var result = host.Services.GetService<ResultContainer>();
            await host.RunAsync();
            result.X.Should().Be(1);
        }

    }
}