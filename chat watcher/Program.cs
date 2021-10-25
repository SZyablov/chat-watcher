using CommandHandlerNS;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace bot____
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().StartAsync().GetAwaiter().GetResult();
        }

        private DiscordSocketClient client;
        private CommandHandler handler;

        public async Task StartAsync()
        {
            client = new DiscordSocketClient();
            await client.LoginAsync(TokenType.Bot, "API-TOKEN");
            await client.StartAsync();
            handler = new CommandHandler(client);
            await Task.Delay(-1);
        }
    }
}
