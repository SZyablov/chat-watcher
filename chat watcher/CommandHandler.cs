using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CommandHandlerNS
{
    class CommandHandler
    {
        public const string version = "Наблюдатель";
        public static string footer = $"{version}";
        public static Discord.Color embedColor = Discord.Color.DarkBlue;
        public static string prefix = "н.";
        public static string rootFolderCW = "../files/cw";
        public static string rootFolderAS = "../files/AS";

        // System variables
        #region system
        public static DiscordSocketClient client { get; private set; }
        private CommandService service;
        private IServiceProvider serviceProv;
        #endregion           

        // Discord Channels IDs
        #region channels
        Dictionary<ulong, ulong> logChannel = new Dictionary<ulong, ulong>()
        { { 357605891656122380,798148114174443520 },{ 296283889100521475,496427720213921802 } };

        static List<ulong> allowedChannels = new List<ulong>()
        {477926816032489484, 852137335453253632, 798148114174443520};
        #endregion

        // Main Roles IDs
        #region roles
        public static ulong creator = 238388347884535818;
        public static ulong kappa = 478531496773287948;
        public static ulong gods = 477918110104420353;
        public static ulong kappaTest = 798244585121775616;
        public static ulong godsTest = 798244640452509706;
        #endregion

        // Word Filter: Variables, methods, etc.
        #region Word Filter
        #region paths
        public static string banwordsFolder = $@"{rootFolderCW}/banwords.xml";
        public static string replaceFolder = $@"{rootFolderCW}/replacers.xml";
        public static string exceptionsFolder = $@"{rootFolderCW}/exceptions.xml";
        public static string filterConfigFolder = $@"{rootFolderCW}/config.xml";
        #endregion

        #region filter settings
        static List<string> banwords = new List<string>();
        static List<Replacer> replacers = new List<Replacer>();
        static List<string> exceptions = new List<string>();

        static bool isFilterEnabled = false;
        static int millis = 5000;
        static bool thumbnailAdding = true;
        static string thumbnailLink = @"https://cdn.discordapp.com/attachments/798148114174443520/827975007388172329/hina_angery.png";
        #endregion

        public class Replacer
        {
            public string toReplace;
            public string replacer;

            public Replacer(string toReplace, string replacer)
            {
                this.toReplace = toReplace;
                this.replacer = replacer;
            }
        }

        void LoadWords()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(banwordsFolder);
            XmlElement root = reportsFile.DocumentElement;

            foreach (XmlNode node in root)
            {
                switch (node.Name)
                {
                    case "delay": millis = int.Parse(node.InnerText); break;
                    case "word": banwords.Add(node.InnerText); break;
                }
            }
        }

        void LoadReplacers()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(replaceFolder);
            XmlElement root = reportsFile.DocumentElement;

            foreach (XmlNode node in root)
            {
                replacers.Add(new Replacer(node.ChildNodes[0].InnerText, node.ChildNodes[1].InnerText));
            }
        }

        void LoadExceptions()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(exceptionsFolder);
            XmlElement root = reportsFile.DocumentElement;

            foreach (XmlNode node in root)
            {
                exceptions.Add(node.InnerText);
            }
        }

        void LoadFilterConfig()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(filterConfigFolder);
            XmlElement root = reportsFile.DocumentElement;

            isFilterEnabled = bool.Parse(root.ChildNodes[0].InnerText);
            millis = int.Parse(root.ChildNodes[1].InnerText);
            thumbnailAdding = bool.Parse(root.ChildNodes[2].InnerText);
            thumbnailLink = root.ChildNodes[3].InnerText;
        }

        static void SaveFilterConfig()
        {
            try
            {
                using (FileStream fs = File.Create(filterConfigFolder))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                  "<config>\n" +
                                                                  "</config>\n");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(filterConfigFolder);
            XmlElement root = doc.DocumentElement;

            XmlElement isFilterEnabledEl = doc.CreateElement("filterEnabled");
            XmlElement millisEl = doc.CreateElement("millis");
            XmlElement thumbnailEnabledEl = doc.CreateElement("thumbnailEnabled");
            XmlElement thumbnailLinkEl = doc.CreateElement("thumbnailLink");
            XmlText isFilterEnabledText = doc.CreateTextNode(isFilterEnabled.ToString());
            XmlText millisText = doc.CreateTextNode(millis.ToString());
            XmlText thumbnailEnabledText = doc.CreateTextNode(thumbnailAdding.ToString());
            XmlText thumbnailLinkText = doc.CreateTextNode(thumbnailLink);
            isFilterEnabledEl.AppendChild(isFilterEnabledText);
            millisEl.AppendChild(millisText);
            thumbnailEnabledEl.AppendChild(thumbnailEnabledText);
            thumbnailLinkEl.AppendChild(thumbnailLinkText);
            root.AppendChild(isFilterEnabledEl);
            root.AppendChild(millisEl);
            root.AppendChild(thumbnailEnabledEl);
            root.AppendChild(thumbnailLinkEl);

            doc.Save(filterConfigFolder);
        }

        public static async void DeleteFilterMessage(object id)
        {
            try
            {
                SocketUserMessage msg = (SocketUserMessage)id;

                EmbedBuilder embed = new EmbedBuilder();
                embed.WithColor(embedColor)
                    .WithDescription($"{msg.Author.Mention}, пожалуйста, следи за словами!");

                if (thumbnailAdding)
                    embed.WithThumbnailUrl(thumbnailLink);

                var messageToDelete = msg.Channel.SendMessageAsync(embed: embed.Build()).Result;
                Console.WriteLine(messageToDelete.Content);
                Thread.Sleep(5000);
                await messageToDelete.DeleteAsync();
            }
            catch
            {
            }
        }
        #endregion Word Filter

        // AntiSpam: Variables, methods, etc.
        #region AntiSpam
        static string exitSentense = "выход";
        static string saveSentense = "сохранить";

        static string antiSpamFolder = $@"{rootFolderAS}/ASConfig.xml";
        static string antiSpamChannelsFolder = $@"{rootFolderAS}/ASChannels.xml";
        static string telemetryFolder = $@"{rootFolderAS}/telemetry/";
        static string reportFolder = $@"{rootFolderAS}/reports/";
        static string reportImage = $@"{rootFolderAS}/reports/output.png";

        static bool isOn = false;
        static bool isMuting = false;
        static bool isTrustedMuting = false;
        static List<ulong> trustedRoles = new List<ulong>();
        static List<string> mediaFileTypes = new List<string>();
        static string mediaFileTypesString = "";
        static bool isLogging = false;
        static bool isLoggingPing = false;
        static ulong loggerChannel = 0;

        static AntiSpam global = new AntiSpam(new AntiSpamBuilder());
        static Dictionary<ulong, AntiSpam> channelsSettings = new Dictionary<ulong, AntiSpam>();

        static Dictionary<ulong, List<Message>> messages = new Dictionary<ulong, List<Message>>();
        static Dictionary<ulong, DateTime> mutedUsers = new Dictionary<ulong, DateTime>();

        static List<Telemetry> telemetry = new List<Telemetry>();
        static Timer saveTimer;
        static string log;

        public class Message
        {
            public float weight { get; private set; }
            public DateTimeOffset expirationTime { get; private set; }

            public Message(float weight, DateTimeOffset expirationTime)
            {
                this.weight = weight;
                this.expirationTime = expirationTime;
            }
        }

        public class Telemetry
        {
            public float weight { get; private set; }
            public string channelName { get; private set; }
            public ulong user { get; private set; }
            public DateTimeOffset dt { get; private set; }

            public Telemetry(float weight, string channelName, ulong user, DateTimeOffset dt)
            {
                this.weight = weight;
                this.channelName = channelName;
                this.user = user;
                this.dt = dt;
            }
        }

        public class AntiSpamBuilder
        {
            public ulong channel = 0;
            public float multipler = 1;
            public float message = 5;
            public float attachment = 5;
            public float link = 2;
            public float linkImage = 5;
            public int observeTime = 30;
            public int limit = 20;
            public int timeout = 60;
            public int trustedReduce = 50;
            public int trustedTimeout = 10;
            public int trustedLimit = 30;

            public int changingSetting;

            public AntiSpamBuilder()
            {

            }

            public AntiSpamBuilder(AntiSpam antispam)
            {
                channel = antispam.channel;
                multipler = antispam.multipler;
                message = antispam.message;
                attachment = antispam.attachment;
                link = antispam.link;
                linkImage = antispam.linkImage;
                observeTime = antispam.observeTime;
                limit = antispam.limit;
                timeout = antispam.timeout;
                trustedReduce = antispam.trustedReduce;
                trustedTimeout = antispam.trustedTimeout;
                trustedLimit = antispam.trustedLimit;
            }
        }

        public class AntiSpam
        {
            public ulong channel { get; private set; }
            public float multipler { get; private set; }
            public float message { get; private set; }
            public float attachment { get; private set; }
            public float link { get; private set; }
            public float linkImage { get; private set; }
            public int observeTime { get; private set; }
            public int limit { get; private set; }
            public int timeout { get; private set; }
            public int trustedReduce { get; private set; }
            public int trustedTimeout { get; private set; }
            public int trustedLimit { get; private set; }

            public AntiSpam(AntiSpamBuilder asb)
            {
                channel = asb.channel;
                multipler = asb.multipler;
                message = asb.message;
                attachment = asb.attachment;
                link = asb.link;
                linkImage = asb.linkImage;
                observeTime = asb.observeTime;
                limit = asb.limit;
                timeout = asb.timeout;
                trustedReduce = asb.trustedReduce;
                trustedTimeout = asb.trustedTimeout;
                trustedLimit = asb.trustedLimit;
            }
        }

        static void SaveAntiSpamConfig()
        {
            try
            {
                using (FileStream fs = File.Create(antiSpamFolder))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                  "<config>\n" +
                                                                  "</config>\n");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            string roles = "";
            foreach (ulong id in trustedRoles)
            {
                roles += $"{id} ";
            }
            string mediaTypes = "";
            foreach (string type in mediaFileTypes)
            {
                mediaTypes += $"{type} ";
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(antiSpamFolder);
            XmlElement root = doc.DocumentElement;

            XmlElement isOnEl = doc.CreateElement("isOn");
            XmlElement isMutingEl = doc.CreateElement("isMuting");
            XmlElement isTrustedMutingEl = doc.CreateElement("isTrustedMuting");
            XmlElement trustedRolesEl = doc.CreateElement("trustedRoles");
            XmlElement mediaFileTypesEl = doc.CreateElement("mediaFileTypes");
            XmlElement isLoggingEl = doc.CreateElement("isLogging");
            XmlElement isLoggingPingEl = doc.CreateElement("isLoggingPing");
            XmlElement loggerChannelEl = doc.CreateElement("loggerChannel");
            XmlText isOnText = doc.CreateTextNode(isOn.ToString());
            XmlText isMutingText = doc.CreateTextNode(isMuting.ToString());
            XmlText isTrustedText = doc.CreateTextNode(isTrustedMuting.ToString());
            XmlText trustedRolesText = doc.CreateTextNode(roles);
            XmlText mediaFileTypesText = doc.CreateTextNode(mediaTypes);
            XmlText isLoggingText = doc.CreateTextNode(isLogging.ToString());
            XmlText isLoggingPingText = doc.CreateTextNode(isLoggingPing.ToString());
            XmlText loggerChannelText = doc.CreateTextNode(loggerChannel.ToString());
            isOnEl.AppendChild(isOnText);
            isMutingEl.AppendChild(isMutingText);
            isTrustedMutingEl.AppendChild(isTrustedText);
            trustedRolesEl.AppendChild(trustedRolesText);
            mediaFileTypesEl.AppendChild(mediaFileTypesText);
            isLoggingEl.AppendChild(isLoggingText);
            isLoggingPingEl.AppendChild(isLoggingPingText);
            loggerChannelEl.AppendChild(loggerChannelText);
            root.AppendChild(isOnEl);
            root.AppendChild(isMutingEl);
            root.AppendChild(isTrustedMutingEl);
            root.AppendChild(trustedRolesEl);
            root.AppendChild(mediaFileTypesEl);
            root.AppendChild(isLoggingEl);
            root.AppendChild(isLoggingPingEl);
            root.AppendChild(loggerChannelEl);

            doc.Save(antiSpamFolder);
        }

        static void SaveAntiSpamChannels()
        {
            try
            {
                using (FileStream fs = File.Create(antiSpamChannelsFolder))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                  "<config>\n" +
                                                                  "</config>\n");
                    fs.Write(info, 0, info.Length);
                    fs.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            string roles = "";
            foreach (ulong id in trustedRoles)
            {
                roles += $"{id} ";
            }
            string mediaTypes = "";
            foreach (string type in mediaFileTypes)
            {
                roles += $"{type} ";
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(antiSpamChannelsFolder);
            XmlElement root = doc.DocumentElement;

            XmlElement globalRoot = doc.CreateElement("channel");

            XmlElement globalchannelEl = doc.CreateElement("channelID");
            XmlElement globalmultiplerEl = doc.CreateElement("multipler");
            XmlElement globalmessageEl = doc.CreateElement("message");
            XmlElement globalattachmentEl = doc.CreateElement("attachment");
            XmlElement globallinkEl = doc.CreateElement("link");
            XmlElement globallinkImageEl = doc.CreateElement("linkImage");
            XmlElement globalobserveTimeEl = doc.CreateElement("observeTime");
            XmlElement globallimitEl = doc.CreateElement("limit");
            XmlElement globaltimeoutEl = doc.CreateElement("timeout");
            XmlElement globaltrustedReduceEl = doc.CreateElement("trustedReduce");
            XmlElement globaltrustedTimeoutEl = doc.CreateElement("trustedTimeout");
            XmlElement globaltrustedLimitEl = doc.CreateElement("trustedLimit");

            XmlText globalchannelText = doc.CreateTextNode(global.channel.ToString());
            XmlText globalmultiplerText = doc.CreateTextNode(global.multipler.ToString());
            XmlText globalmessageText = doc.CreateTextNode(global.message.ToString());
            XmlText globalattachmentText = doc.CreateTextNode(global.attachment.ToString());
            XmlText globallinkText = doc.CreateTextNode(global.link.ToString());
            XmlText globallinkImageText = doc.CreateTextNode(global.linkImage.ToString());
            XmlText globalobserveTimeText = doc.CreateTextNode(global.observeTime.ToString());
            XmlText globallimitText = doc.CreateTextNode(global.limit.ToString());
            XmlText globaltimeoutText = doc.CreateTextNode(global.timeout.ToString());
            XmlText globaltrustedReduceText = doc.CreateTextNode(global.trustedReduce.ToString());
            XmlText globaltrustedTimeoutText = doc.CreateTextNode(global.trustedTimeout.ToString());
            XmlText globaltrustedLimitText = doc.CreateTextNode(global.trustedLimit.ToString());

            globalchannelEl.AppendChild(globalchannelText);
            globalmultiplerEl.AppendChild(globalmultiplerText);
            globalmessageEl.AppendChild(globalmessageText);
            globalattachmentEl.AppendChild(globalattachmentText);
            globallinkEl.AppendChild(globallinkText);
            globallinkImageEl.AppendChild(globallinkImageText);
            globalobserveTimeEl.AppendChild(globalobserveTimeText);
            globallimitEl.AppendChild(globallimitText);
            globaltimeoutEl.AppendChild(globaltimeoutText);
            globaltrustedReduceEl.AppendChild(globaltrustedReduceText);
            globaltrustedTimeoutEl.AppendChild(globaltrustedTimeoutText);
            globaltrustedLimitEl.AppendChild(globaltrustedLimitText);

            globalRoot.AppendChild(globalchannelEl);
            globalRoot.AppendChild(globalmultiplerEl);
            globalRoot.AppendChild(globalmessageEl);
            globalRoot.AppendChild(globalattachmentEl);
            globalRoot.AppendChild(globallinkEl);
            globalRoot.AppendChild(globallinkImageEl);
            globalRoot.AppendChild(globalobserveTimeEl);
            globalRoot.AppendChild(globallimitEl);
            globalRoot.AppendChild(globaltimeoutEl);
            globalRoot.AppendChild(globaltrustedReduceEl);
            globalRoot.AppendChild(globaltrustedTimeoutEl);
            globalRoot.AppendChild(globaltrustedLimitEl);

            root.AppendChild(globalRoot);

            foreach (AntiSpam aS in channelsSettings.Values)
            {
                XmlElement channelRoot = doc.CreateElement("channel");

                XmlElement channelEl = doc.CreateElement("channelID");
                XmlElement multiplerEl = doc.CreateElement("multipler");
                XmlElement messageEl = doc.CreateElement("message");
                XmlElement attachmentEl = doc.CreateElement("attachment");
                XmlElement linkEl = doc.CreateElement("link");
                XmlElement linkImageEl = doc.CreateElement("linkImage");
                XmlElement observeTimeEl = doc.CreateElement("observeTime");
                XmlElement limitEl = doc.CreateElement("limit");
                XmlElement timeoutEl = doc.CreateElement("timeout");
                XmlElement trustedReduceEl = doc.CreateElement("trustedReduce");
                XmlElement trustedTimeoutEl = doc.CreateElement("trustedTimeout");
                XmlElement trustedLimitEl = doc.CreateElement("trustedLimit");

                XmlText channelText = doc.CreateTextNode(aS.channel.ToString());
                XmlText multiplerText = doc.CreateTextNode(aS.multipler.ToString());
                XmlText messageText = doc.CreateTextNode(aS.message.ToString());
                XmlText attachmentText = doc.CreateTextNode(aS.attachment.ToString());
                XmlText linkText = doc.CreateTextNode(aS.link.ToString());
                XmlText linkImageText = doc.CreateTextNode(aS.linkImage.ToString());
                XmlText observeTimeText = doc.CreateTextNode(aS.observeTime.ToString());
                XmlText limitText = doc.CreateTextNode(aS.limit.ToString());
                XmlText timeoutText = doc.CreateTextNode(aS.timeout.ToString());
                XmlText trustedReduceText = doc.CreateTextNode(aS.trustedReduce.ToString());
                XmlText trustedTimeoutText = doc.CreateTextNode(aS.trustedTimeout.ToString());
                XmlText trustedLimitText = doc.CreateTextNode(aS.trustedLimit.ToString());

                channelEl.AppendChild(channelText);
                multiplerEl.AppendChild(multiplerText);
                messageEl.AppendChild(messageText);
                attachmentEl.AppendChild(attachmentText);
                linkEl.AppendChild(linkText);
                linkImageEl.AppendChild(linkImageText);
                observeTimeEl.AppendChild(observeTimeText);
                limitEl.AppendChild(limitText);
                timeoutEl.AppendChild(timeoutText);
                trustedReduceEl.AppendChild(trustedReduceText);
                trustedTimeoutEl.AppendChild(trustedTimeoutText);
                trustedLimitEl.AppendChild(trustedLimitText);

                channelRoot.AppendChild(channelEl);
                channelRoot.AppendChild(multiplerEl);
                channelRoot.AppendChild(messageEl);
                channelRoot.AppendChild(attachmentEl);
                channelRoot.AppendChild(linkEl);
                channelRoot.AppendChild(linkImageEl);
                channelRoot.AppendChild(observeTimeEl);
                channelRoot.AppendChild(limitEl);
                channelRoot.AppendChild(timeoutEl);
                channelRoot.AppendChild(trustedReduceEl);
                channelRoot.AppendChild(trustedTimeoutEl);
                channelRoot.AppendChild(trustedLimitEl);

                root.AppendChild(channelRoot);
            }

            doc.Save(antiSpamChannelsFolder);
        }

        void LoadAntiSpamConfig()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(antiSpamFolder);
            XmlElement root = reportsFile.DocumentElement;

            foreach (XmlNode node in root)
            {
                switch (node.Name)
                {
                    case "isOn": isOn = bool.Parse(node.InnerText); break;
                    case "isMuting": isMuting = bool.Parse(node.InnerText); break;
                    case "isTrustedMuting": isTrustedMuting = bool.Parse(node.InnerText); break;
                    case "trustedRoles":
                        foreach (Match match in Regex.Matches(node.InnerText, @"(\d*)\s"))
                        {
                            trustedRoles.Add(ulong.Parse(match.Groups[1].Value));
                        }
                        break;
                    case "mediaFileTypes":
                        foreach (Match match in Regex.Matches(node.InnerText, @"(\w*)\s"))
                        {
                            mediaFileTypes.Add(match.Groups[1].Value.Replace(" ", ""));
                        }
                        foreach (string type in mediaFileTypes)
                        {
                            mediaFileTypesString += $"{type}|";
                        }
                        mediaFileTypesString = Regex.Replace(mediaFileTypesString, @"\|$", "");
                        break;
                    case "isLogging": isLogging = bool.Parse(node.InnerText); break;
                    case "isLoggingPing": isLoggingPing = bool.Parse(node.InnerText); break;
                    case "loggerChannel": loggerChannel = ulong.Parse(node.InnerText); break;
                }
            }
        }

        void LoadAntiSpamChannels()
        {
            XmlDocument reportsFile = new XmlDocument();
            reportsFile.Load(antiSpamChannelsFolder);
            XmlElement root = reportsFile.DocumentElement;

            foreach (XmlNode channel in root)
            {
                AntiSpamBuilder asb = new AntiSpamBuilder();

                foreach (XmlNode node in channel.ChildNodes)
                {
                    switch (node.Name)
                    {
                        case "channelID": asb.channel = ulong.Parse(node.InnerText); break;
                        case "multipler": asb.multipler = float.Parse(node.InnerText); break;
                        case "message": asb.message = float.Parse(node.InnerText); break;
                        case "attachment": asb.attachment = float.Parse(node.InnerText); break;
                        case "link": asb.link = float.Parse(node.InnerText); break;
                        case "linkImage": asb.linkImage = float.Parse(node.InnerText); break;
                        case "observeTime": asb.observeTime = int.Parse(node.InnerText); break;
                        case "limit": asb.limit = int.Parse(node.InnerText); break;
                        case "timeout": asb.timeout = int.Parse(node.InnerText); break;
                        case "trustedReduce": asb.trustedReduce = int.Parse(node.InnerText); break;
                        case "trustedTimeout": asb.trustedTimeout = int.Parse(node.InnerText); break;
                        case "trustedLimit": asb.trustedLimit = int.Parse(node.InnerText); break;
                    }
                }

                if (asb.channel == 0) global = new AntiSpam(asb);
                else channelsSettings.Add(asb.channel, new AntiSpam(asb));
            }
        }

        private void SaveTelemetry(object obj)
        {
            string filename = $@"{telemetryFolder}{DateTime.Now.ToString("dd-MM-yyyy")}.xml";
            if (!File.Exists(filename))
                try
                {
                    using (FileStream fs = File.Create(filename))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                      "<config>\n" +
                                                                      "</config>\n");
                        fs.Write(info, 0, info.Length);
                        fs.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            XmlDocument doc = new XmlDocument();
            doc.Load(filename);
            XmlElement root = doc.DocumentElement;

            foreach (Telemetry tm in telemetry)
            {
                XmlElement line = doc.CreateElement("line");
                XmlElement userEl = doc.CreateElement("user");
                XmlElement channelEl = doc.CreateElement("channel");
                XmlElement weightEl = doc.CreateElement("weight");
                XmlElement dateEl = doc.CreateElement("date");
                XmlText userText = doc.CreateTextNode(tm.user.ToString());
                XmlText channelText = doc.CreateTextNode(tm.channelName);
                XmlText weightText = doc.CreateTextNode(tm.weight.ToString());
                XmlText dateText = doc.CreateTextNode(tm.dt.ToString());

                userEl.AppendChild(userText);
                channelEl.AppendChild(channelText);
                weightEl.AppendChild(weightText);
                dateEl.AppendChild(dateText);
                line.AppendChild(userEl);
                line.AppendChild(channelEl);
                line.AppendChild(weightEl);
                line.AppendChild(dateEl);
                root.AppendChild(line);
            }

            doc.Save(filename);

            telemetry.Clear();
        }

        private class TelemetryMessages
        {
            public float weight { get; private set; }
            public ulong user { get; private set; }
            public DateTimeOffset dt { get; private set; }

            public TelemetryMessages(float weight, ulong user, DateTimeOffset dt)
            {
                this.weight = weight;
                this.user = user;
                this.dt = dt;
            }
        }
        private class Point
        {
            public float weight { get; private set; }
            public DateTimeOffset pointTime { get; private set; }

            public Point(float weight, DateTimeOffset pointTime)
            {
                this.weight = weight;
                this.pointTime = pointTime;
            }
        }
        private static void CreateReport(List<TelemetryMessages> messagesList, string channelName, string date)
        {
            try
            {
                ulong chID = 0;
                SocketGuild guild = client.GetGuild(296283889100521475);
                foreach (SocketGuildChannel ch in guild.Channels)
                    if (ch.Name == channelName) chID = ch.Id;

                Bitmap outputImage = new Bitmap(600, 400, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                Graphics graphics = Graphics.FromImage(outputImage);
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                //graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, 600, 400));
                graphics.DrawImage(new Bitmap($"{reportFolder}hinagraphics.png"), new Rectangle(0, 0, 600, 400));

                List<Point> allTotalWeights = new List<Point>();
                Dictionary<ulong, List<Message>> messagesSim = new Dictionary<ulong, List<Message>>();

                AntiSpam aS;
                DateTime today = new DateTime();

                if (channelsSettings.ContainsKey(chID))
                    aS = channelsSettings[chID];
                else aS = global;

                //Console.WriteLine(messagesList.Count);
                foreach (TelemetryMessages tms in messagesList)
                {
                    if (tms.dt.ToString("dd-MM-yyyy") != date) continue;

                    bool isTrusted = false;
                    DateTimeOffset dt = tms.dt;
                    float totalWeight = tms.weight;
                    int limit = 0;
                    int timeout = 0;

                    if (!messages.ContainsKey(tms.user))
                        messages.Add(tms.user, new List<Message>());

                    List<SocketRole> roles;
                    try
                    {
                        roles = guild.GetUser(tms.user).Roles.ToList();
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                    foreach (SocketRole role in roles)
                    {
                        foreach (ulong id in trustedRoles)
                        {
                            if (role.Id == id)
                            {
                                isTrusted = true;
                                break;
                            }
                        }
                        if (isTrusted) break;
                    }

                    if (isTrusted)
                    {
                        limit = aS.trustedLimit;
                        timeout = aS.trustedTimeout;
                    }
                    else
                    {
                        limit = aS.limit;
                        timeout = aS.timeout;
                    }
                    List<int> indexes = new List<int>();
                    int i = 0;
                    foreach (Message message in messages[tms.user])
                    {
                        if (dt >= message.expirationTime)
                            indexes.Add(i);
                        i++;
                    }

                    int deleted = 0;
                    foreach (int index in indexes)
                    {
                        messages[tms.user].RemoveAt(index - deleted);
                        deleted++;
                    }

                    messages[tms.user].Add(new Message(totalWeight, dt.AddSeconds(aS.observeTime)));

                    float summaryWeight = 0.0f;
                    foreach (Message message in messages[tms.user])
                    {
                        summaryWeight += message.weight;
                    }

                    allTotalWeights.Add(new Point(summaryWeight, dt));
                }
                allTotalWeights.Reverse();
                allTotalWeights.Add(new Point(0, allTotalWeights[allTotalWeights.Count - 1].pointTime));
                allTotalWeights.Reverse();
                allTotalWeights.Add(new Point(0, allTotalWeights[allTotalWeights.Count - 1].pointTime));

                string numberFont = "Corbel";
                float numberSize = 10;
                StringFormat stringFormat = new StringFormat();
                stringFormat.Alignment = StringAlignment.Center;
                stringFormat.LineAlignment = StringAlignment.Center;
                for (int i = 0; i < 24; i++)
                {
                    int x1 = (int)(i / 23.0f * 520) + 40;
                    int y1 = 370;
                    graphics.DrawString(i.ToString(), new Font(numberFont, numberSize), Brushes.Black, new PointF(x1, y1), stringFormat);

                    graphics.DrawLine(Pens.Gray, x1, 40, x1, 370);
                }
                for (int i = 1; i < 5; i++)
                {
                    int x1 = 30;
                    int y1 = -(int)((i) / 4.0f * 300) + 360;
                    graphics.DrawString(((i / 4.0f) * aS.limit).ToString(), new Font(numberFont, numberSize), Brushes.Black, new PointF(x1, y1), stringFormat);

                    graphics.DrawLine(Pens.Gray, 40, y1, 560, y1);
                }
                float textSize = 15;
                graphics.DrawString("Часы", new Font(numberFont, textSize), Brushes.Black, new PointF(300, 388), stringFormat);
                graphics.DrawString($"График весовой активности в #{channelName}", new Font(numberFont, textSize), Brushes.Black, new PointF(300, 22), stringFormat);
                graphics.TranslateTransform(15, 200);
                graphics.RotateTransform(-90f);
                graphics.DrawString("Максимальные весы", new Font(numberFont, textSize), Brushes.Black, new PointF(0, 0), stringFormat);
                graphics.ResetTransform();

                TimeSpan startTime = new TimeSpan(allTotalWeights[0].pointTime.Hour, allTotalWeights[0].pointTime.Minute, allTotalWeights[0].pointTime.Second);
                TimeSpan endTime = new TimeSpan(23, 59, 59);
                Pen pen = new Pen(System.Drawing.Color.Aquamarine, 2);
                for (int i = 0; i < allTotalWeights.Count - 1; i++)
                {
                    startTime = new TimeSpan(allTotalWeights[i].pointTime.Hour, allTotalWeights[i].pointTime.Minute, allTotalWeights[i].pointTime.Second);
                    float x1 = (float)(startTime.TotalSeconds / endTime.TotalSeconds * 520) + 40;
                    float y1 = -((allTotalWeights[i].weight / aS.limit) * 300) + 360;
                    startTime = new TimeSpan(allTotalWeights[i + 1].pointTime.Hour, allTotalWeights[i + 1].pointTime.Minute, allTotalWeights[i + 1].pointTime.Second);
                    float x2 = (float)(startTime.TotalSeconds / endTime.TotalSeconds * 520) + 40;
                    float y2 = -((allTotalWeights[i + 1].weight / aS.limit) * 300) + 360;
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                    //Console.WriteLine("{0} {1} {2} {3}", x1, y1, startTime, endTime);
                }

                graphics.DrawLine(Pens.Black, 40, 40, 40, 360);
                graphics.DrawLine(Pens.Black, 40, 360, 560, 360);

                outputImage.Save(reportImage);
                Console.WriteLine();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
        }

        private class ToDeleteMessage
        {
            public SocketUserMessage msg { get; private set; }
            public int timeout { get; private set; }

            public ToDeleteMessage(SocketUserMessage msg, int timeout)
            {
                this.msg = msg;
                this.timeout = timeout;
            }
        }
        public static async void DeleteASMessage(object id)
        {
            try
            {
                ToDeleteMessage msg = (ToDeleteMessage)id;

                AntiSpam aS = null;

                EmbedBuilder embed = new EmbedBuilder()
                .WithDescription($"{msg.msg.Author.Mention}, ты отправляешь слишком много объёмных сообщений! Полегче, пожалуйста!\n" +
                $"Подожди {msg.timeout} секунд перед тем как ты сможешь снова отправлять сообщения")
                .WithColor(embedColor);

                if (thumbnailAdding)
                    embed.WithThumbnailUrl(thumbnailLink);

                var messageToDelete = msg.msg.Channel.SendMessageAsync(embed: embed.Build()).Result;
                Console.WriteLine(messageToDelete.Content);
                Thread.Sleep(msg.timeout * 1000);
                await messageToDelete.DeleteAsync();
            }
            catch
            {
            }
        }
        #endregion

        // CommandHandler constructor
        public CommandHandler(DiscordSocketClient _client)
        {
            client = _client;

            serviceProv = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton<InteractiveService>()
                .BuildServiceProvider();
            service = new CommandService();
            service.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProv);
            client.MessageReceived += HandleCommandAsync;
            client.ReactionAdded += ReactionAdded;
            client.MessageUpdated += HandleEditAsync;

            LoadWords();
            LoadReplacers();
            LoadExceptions();
            LoadFilterConfig();

            LoadAntiSpamConfig();
            LoadAntiSpamChannels();

            TimeSpan mils = new DateTime(1, 1, 1, DateTime.Now.Hour, 0, 0).AddHours(1).AddSeconds(-30) - new DateTime(1, 1, 1, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            saveTimer = new Timer(new TimerCallback(SaveTelemetry), null, (int)mils.TotalMilliseconds, 3600000);

            Console.WriteLine($"[{DateTime.Now}] {version} launched");
        }

        // Fires when someone sends message
        private async Task HandleCommandAsync(SocketMessage s)
        {
            var msg = s as SocketUserMessage;
            if (msg == null) return;
            var context = new SocketCommandContext(client, msg);
            int argPos = 0;

            if (msg.HasStringPrefix(prefix, ref argPos))
            {
                var result = await service.ExecuteAsync(context, argPos, serviceProv);

                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
                {
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                    Console.WriteLine(result.ErrorReason);
                }
            }

            HandleWordFilter(s);
            HandleAntiSpam(s);
        }
        // Fires when someone edits message
        private async Task HandleEditAsync(Cacheable<IMessage, ulong> cache, SocketMessage s, ISocketMessageChannel channel)
        {
            HandleWordFilter(s);
        }
        // Finds banworns in message
        private async Task HandleWordFilter(SocketMessage s)
        {
            var msg = s as SocketUserMessage;
            if (msg == null) return;
            var context = new SocketCommandContext(client, msg);

            #region word filter
            try
            {
                if (isFilterEnabled)
                    if (!IsAdmin(msg.Author as SocketGuildUser, "", AccessLevel.Kappa) && !msg.Author.IsBot)
                    {
                        SocketGuild guild = null;
                        try
                        {
                            guild = ((SocketGuildUser)msg.Author).Guild;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.StackTrace);
                            Console.WriteLine(ex.Message);
                        }
                        string messageStr = msg.Content.ToLower();
                        Thread delete = new Thread(new ParameterizedThreadStart(DeleteFilterMessage));

                        foreach (string word in exceptions)
                        {
                            messageStr = messageStr.Replace(word, "");
                        }

                        foreach (string word in banwords)
                        {
                            if (messageStr.Contains(word))
                            {
                                msg.DeleteAsync();
                                delete.Start(msg);
                                client.GetGuild(guild.Id).GetTextChannel(logChannel[guild.Id])
                                    .SendMessageAsync($"{msg.Author} отправил в <#{msg.Channel.Id}> сообщение с запрещённым словом:\n`{msg.Content}`");
                                return;
                            }
                        }

                        foreach (Replacer replacer in replacers)
                        {
                            messageStr = messageStr.Replace(replacer.toReplace, replacer.replacer);
                        }

                        foreach (string word in banwords)
                        {
                            if (messageStr.Contains(word))
                            {
                                msg.DeleteAsync();
                                delete.Start(msg);
                                client.GetGuild(guild.Id).GetTextChannel(logChannel[guild.Id])
                                    .SendMessageAsync($"{msg.Author} отправил в <#{msg.Channel.Id}> сообщение с запрещённым словом:\n`{msg.Content}`");
                                return;
                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Message);
            }
            #endregion
        }
        // Checks out if user is spamming
        private void HandleAntiSpam(SocketMessage s)
        {
            var msg = s as SocketUserMessage;
            if (msg == null) return;
            var context = new SocketCommandContext(client, msg);

            if (!isOn) return;
            if (msg.Author.IsBot) return;
            if (msg.Author.IsWebhook) return;
            if (IsAdmin(msg.Author as SocketGuildUser, "", AccessLevel.Kappa)) return;

            DateTime startDT = DateTime.Now;
            Thread delete = new Thread(new ParameterizedThreadStart(DeleteASMessage));

            if (!messages.ContainsKey(msg.Author.Id))
                messages.Add(msg.Author.Id, new List<Message>());

            if (mutedUsers.ContainsKey(msg.Author.Id))
            {
                if (DateTime.Now < mutedUsers[msg.Author.Id])
                {
                    msg.DeleteAsync();
                    return;
                }
                else
                {
                    mutedUsers.Remove(msg.Author.Id);
                }
            }

            float weight = 0.0f;
            float totalWeight = 0.0f;
            DateTimeOffset dt = msg.CreatedAt;
            bool isTrusted = false;
            int limit;
            int timeout;
            AntiSpam aS = null;

            if (channelsSettings.ContainsKey(context.Channel.Id))
                aS = channelsSettings[context.Channel.Id];
            else aS = global;

            weight = (msg.Content.Length / 2000f) * aS.message * aS.multipler;
            totalWeight = weight;

            if (msg.Attachments.Count > 0)
                weight = aS.attachment * aS.multipler;
            if (weight > totalWeight) totalWeight = weight;

            if (Regex.IsMatch(msg.Content, @"(http|https):\/\/[\S]*\.[\w]*\/"))
                weight = aS.link * aS.multipler;
            if (weight > totalWeight) totalWeight = weight;

            if (Regex.IsMatch(msg.Content, @$"(http|https):\/\/[\S]*\.[\w]*\/[\S]*\.({mediaFileTypesString})"))
                weight = aS.linkImage * aS.multipler;
            if (weight > totalWeight) totalWeight = weight;

            List<SocketRole> roles;
            try
            {
                roles = context.Guild.GetUser(msg.Author.Id).Roles.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(msg.GetJumpUrl());
                return;
            }
            foreach (SocketRole role in roles)
            {
                foreach (ulong id in trustedRoles)
                {
                    if (role.Id == id)
                    {
                        isTrusted = true;
                        break;
                    }
                }
                if (isTrusted) break;
            }

            if (isTrusted)
            {
                totalWeight *= aS.trustedReduce / 100f;
                limit = aS.trustedLimit;
                timeout = aS.trustedTimeout;
            }
            else
            {
                limit = aS.limit;
                timeout = aS.timeout;
            }
            List<int> indexes = new List<int>();
            int i = 0;
            foreach (Message message in messages[msg.Author.Id])
            {
                if (dt >= message.expirationTime)
                    indexes.Add(i);
                i++;
            }

            int deleted = 0;
            foreach (int index in indexes)
            {
                messages[msg.Author.Id].RemoveAt(index - deleted);
                deleted++;
            }

            messages[msg.Author.Id].Add(new Message(totalWeight, dt.AddSeconds(aS.observeTime)));

            float summaryWeight = 0.0f;
            foreach (Message message in messages[msg.Author.Id])
            {
                summaryWeight += message.weight;
            }


            if (summaryWeight >= limit)
            {
                if (isMuting)
                {
                    mutedUsers.Add(msg.Author.Id, DateTime.Now.AddSeconds(timeout));
                    delete.Start(new ToDeleteMessage(msg, timeout));
                }

                if (isLogging)
                {
                    EmbedBuilder embed = new EmbedBuilder()
                .WithTitle($"Антиспам лог ({msg.Author.Id})")
                .WithDescription($"{msg.Author.Mention} превысил лимит в канале <#{msg.Channel.Id}>\n" +
                $"Доверенный: {isTrusted}\n" +
                $"Лимит в канале: {limit}\n" +
                $"Таймаут в канале: {timeout}\n" +
                $"[Ссылка на сообщение, спровоцировавшее превышение]({msg.GetJumpUrl()})");

                    client.GetGuild(296283889100521475).GetTextChannel(loggerChannel).SendMessageAsync(isLoggingPing ? "Тест пинга" : "", embed: embed.Build());
                }
            }

            telemetry.Add(new Telemetry(totalWeight, msg.Channel.Name, msg.Author.Id, msg.CreatedAt));
            //Console.Write(msg.Content + "\n");
            //Console.Write($"{(DateTime.Now - startDT).TotalMilliseconds:f2} ms|U:{msg.Author,20}|W:{totalWeight:f3}|CH:{msg.Channel.Name,16}|SUM:{summaryWeight:f2}\n");
            log = $"{(DateTime.Now - startDT).TotalMilliseconds:f2} ms|U:{msg.Author}|W:{totalWeight:f3}|CH:{msg.Channel.Name}|SUM:{summaryWeight:f2}\n" + log;
            log = log.Substring(0, 2000);
        }
        // Fires when someone adds reaction to a message
        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> userMessage, ISocketMessageChannel channel, SocketReaction reaction)
        {
            try
            {
                IUserMessage message = await userMessage.GetOrDownloadAsync();
                SocketGuildUser user = message.Author as SocketGuildUser;
                #region word filter
                if (message.Embeds.Count > 0)
                    if (message.Embeds.ElementAt(0).Title == "Конфигурация фильтра")
                    {
                        if (!reaction.User.Value.IsBot)
                        {
                            if (reaction.Emote.Name == new Emoji("1️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "filter switch", AccessLevel.Kappa))
                                {
                                    isFilterEnabled = !isFilterEnabled;
                                    SaveFilterConfig();
                                    message.ModifyAsync(x => x.Embed = GetFilterConfigEmbed().Build());
                                }
                            if (reaction.Emote.Name == new Emoji("2️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "thumbnail switch", AccessLevel.Kappa))
                                {
                                    thumbnailAdding = !thumbnailAdding;
                                    SaveFilterConfig();
                                    message.ModifyAsync(x => x.Embed = GetFilterConfigEmbed().Build());
                                }
                            if (reaction.Emote.Name == new Emoji("🔄").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "", AccessLevel.Kappa))
                                {
                                    message.ModifyAsync(x => x.Embed = GetFilterConfigEmbed().Build());
                                }
                            message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                        }
                    }
                #endregion
                #region antispam
                if (message.Embeds.Count > 0)
                    if (message.Embeds.ElementAt(0).Title == "Конфигурация антиспама")
                    {
                        if (!reaction.User.Value.IsBot)
                        {
                            if (reaction.Emote.Name == new Emoji("1️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "antispam switch", AccessLevel.Kappa))
                                {
                                    isOn = !isOn;
                                    SaveAntiSpamConfig();
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("2️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "muting switch", AccessLevel.Kappa))
                                {
                                    isMuting = !isMuting;
                                    SaveAntiSpamConfig();
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("3️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "trusted muting switch", AccessLevel.Kappa))
                                {
                                    isTrustedMuting = !isTrustedMuting;
                                    SaveAntiSpamConfig();
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("6️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "logging switch", AccessLevel.Kappa))
                                {
                                    isLogging = !isLogging;
                                    SaveAntiSpamConfig();
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("7️⃣").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "logging ping switch", AccessLevel.Kappa))
                                {
                                    isLoggingPing = !isLoggingPing;
                                    SaveAntiSpamConfig();
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("◀️").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "", AccessLevel.Kappa))
                                {
                                    if (channelsSettings.Count == 0) return;
                                    int i = int.Parse(Regex.Match(message.Embeds.ElementAt(0).Footer.Value.Text, @"page=(\d)").Groups[1].Value) - 1;
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild, i).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("▶️").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "", AccessLevel.Kappa))
                                {
                                    if (channelsSettings.Count == 0) return;
                                    int i = int.Parse(Regex.Match(message.Embeds.ElementAt(0).Footer.Value.Text, @"page=(\d)").Groups[1].Value) + 1;
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild, i).Build());
                                }
                            if (reaction.Emote.Name == new Emoji("🔄").Name)
                                if (IsAdmin(reaction.User.Value as SocketGuildUser, "", AccessLevel.Kappa))
                                {
                                    message.ModifyAsync(x => x.Embed = GetAntiSpamEmbed(message, user.Guild).Build());
                                }
                            message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                        }
                    }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.StackTrace}\n{ex.Message}");
            }
        }

        // Embed builder for Word Filter dashboard
        static EmbedBuilder GetFilterConfigEmbed()
        {
            EmbedBuilder embed = new EmbedBuilder();
            embed.WithColor(embedColor);
            embed.WithFooter(footer);
            embed.WithTitle($"Конфигурация фильтра");
            embed.WithDescription($"Нажатие по цифрам в реакциях переключают соответствующие настройки, нажатие по 🔄 обновляет данную таблицу");
            embed.AddField("> Настройки, переключаемые реакцией:", "￰");
            embed.AddField("Настройка",
               "1️⃣ Фильтр\n2️⃣Миниатюра", true);
            embed.AddField("Состояние",
               $"{BoolToString(isFilterEnabled)}\n{BoolToString(thumbnailAdding)}", true);
            embed.AddField("> Настройки, изменяемые командой:", "￰");
            embed.AddField("Настройка:",
               "Задержка перед удалением\nМиниатюра", true);
            embed.AddField("Значение", $"{millis}мс\n[Ссылка]({thumbnailLink})", true);
            embed.AddField("Команда", "у.бвз <число>\nу.мин <ссылка>", true);

            return embed;

            static string BoolToString(bool value)
            {
                if (value) return "🟢Включено";
                return "🔴Выключено";
            }
        }
        // Embed builder for AntiSpam dashboard
        static EmbedBuilder GetAntiSpamEmbed(IMessage context, SocketGuild guild, int i = 0)
        {
            if (i >= channelsSettings.Count) i = 0;
            else if (i < 0) i = channelsSettings.Count - 1;

            EmbedBuilder embed = new EmbedBuilder();
            embed.WithColor(embedColor);
            embed.WithFooter(footer + $"\npage={i}");
            embed.WithTitle($"Конфигурация антиспама");
            embed.WithDescription($"Нажатие по цифрам в реакциях переключают соответствующие настройки. Нажатие по 🔄 обновит данные в этом сообщении\n" +
                $"• {prefix}доб - открывает окно с добавлением нового канала с настройками\n" +
                $"• {prefix}ред - открывает окно с редактированием настроек\n" +
                $"• {prefix}уда - открывает окно с выбором настроек канала к удалению\n" +
                $"• {prefix}дуб - открывает окно с дубликатом выбранных настроек, чтобы применить их к новому каналу\n\n" +
                $"• {prefix}дов <параметры> - ввод \"+@роль\" добавляет роль, \"-@роль\" убирает роль. Можно вводить несколько через пробел. Пример: `н.дов +@Youkai -@Fairy`\n" +
                $"• {prefix}медиа <параметры>- ввод \"+медиатип\" добавляет вхождение, \"-медиатип\" убирает вхождение. Можно вводить несколько через пробел. Пример: `н.медиа +png -wav`\n" +
                $"• {prefix}лог <канал> - меняет канал для логов\n\n" +
                $"Нажатие на цифры переключают настройки (вкл/выкл)");

            embed.AddField("> Настройки антиспама:", "￰");
            embed.AddField("Настройка",
               "1️⃣Фильтр\n└2️⃣Выдача мута\n└└3️⃣Выдача мута доверенным\n└4️⃣Доверенные роли\n└5️⃣Типы медиафайлов\n" +
               "└6️⃣Запись в логи\n└└7️⃣Пинг в логах\n└└8️⃣Канал логов", true);

            embed.AddField("Состояние",
               $"{BTS(isOn)}\n{BTS(isMuting)}\n{BTS(isTrustedMuting)}\n{TR()}\n" +
               $"{MTF()}\n{BTS(isLogging)}\n{BTS(isLoggingPing)}\n<#{loggerChannel}>", true);

            embed.AddField("> Глобальные настройки:",
                $"Множитель: {global.multipler}\n" +
                $"Вес сообщения: {global.message}\n" +
                $"Вес прикреплённого файла: {global.attachment}\n" +
                $"Вес ссылки: {global.link}\n" +
                $"Вес ссылки с файлом: {global.linkImage}\n" +
                $"Время отслеживания: {global.observeTime}\n" +
                $"Лимит: {global.limit}\n" +
                $"Таймаут: {global.timeout}\n" +
                $"Сокращение веса доверенной роли: {global.trustedReduce}%\n" +
                $"Лимит доверенной роли: {global.trustedLimit}\n" +
                $"Таймаут доверенной роли: {global.trustedTimeout}", true);

            if (channelsSettings.Count > 0)
            {
                embed.AddField($"> {guild.GetTextChannel(channelsSettings.ElementAt(i).Key).Name}:",
                $"Множитель: {channelsSettings.ElementAt(i).Value.multipler}\n" +
                $"Вес сообщения: {channelsSettings.ElementAt(i).Value.message}\n" +
                $"Вес прикреплённого файла: {channelsSettings.ElementAt(i).Value.attachment}\n" +
                $"Вес ссылки: {channelsSettings.ElementAt(i).Value.link}\n" +
                $"Вес ссылки с файлом: {channelsSettings.ElementAt(i).Value.linkImage}\n" +
                $"Время отслеживания: {channelsSettings.ElementAt(i).Value.observeTime}\n" +
                $"Лимит: {channelsSettings.ElementAt(i).Value.limit}\n" +
                $"Таймаут: {channelsSettings.ElementAt(i).Value.timeout}\n" +
                $"Сокращение веса доверенной роли: {channelsSettings.ElementAt(i).Value.trustedReduce}%\n" +
                $"Лимит доверенной роли: {channelsSettings.ElementAt(i).Value.trustedLimit}\n" +
                $"Таймаут доверенной роли: {channelsSettings.ElementAt(i).Value.trustedTimeout}", true);

                string channelList = "";
                for (int index = 0; index < channelsSettings.Count; index++)
                {
                    if (index == i) channelList += $"• **{index + 1}) {guild.GetTextChannel(channelsSettings.ElementAt(index).Key).Name}**\n";
                    else channelList += $"{index + 1}) {guild.GetTextChannel(channelsSettings.ElementAt(index).Key).Name}\n";
                }

                embed.AddField($"> Список каналов:",
                    $"{channelList}", true);
            }

            return embed;

            static string BTS(bool value)
            {
                if (value) return "🟢Включено";
                return "🔴Выключено";
            }

            static string TR()
            {
                string result = "";
                foreach (ulong id in trustedRoles)
                {
                    result += $"<@&{id}> ";
                }

                return result;
            }

            static string MTF()
            {
                string result = "";
                foreach (string type in mediaFileTypes)
                {
                    result += $"{type}, ";
                }
                result = Regex.Replace(result, @",\s$", "");

                return result;
            }
        }

        public class Commands : InteractiveBase
        {
            // Help command
            [Command("помощь")]
            [Alias("ап")]
            public async Task Help()
            {
                try
                {
                    SocketGuild guild = Context.Guild;
                    if (IsAdmin(Context.User as SocketGuildUser, "", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            EmbedBuilder embed = new EmbedBuilder();
                            embed.WithColor(embedColor)
                                .WithFooter(footer)
                                .WithTitle($"Помощь по утилитам")
                                .AddField($"> `{prefix}помощьфильтр ({prefix}пф)`",
                                "**Описание:** Показывает команды, относящиеся к вордфильтру")
                                .AddField($"> `{prefix}помощьантиспам ({prefix}па)`",
                                "**Описание:** Показывает команды, относящиеся к антиспам системе");

                            Context.Channel.SendMessageAsync(embed: embed.Build());
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            #region Word Filter
            // Word Filter help command
            [Command("помощьфильтр")]
            [Alias("пф")]
            public async Task HelpFilter()
            {
                try
                {
                    SocketGuild guild = Context.Guild;
                    if (IsAdmin(Context.User as SocketGuildUser, "", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            EmbedBuilder embed = new EmbedBuilder();
                            embed.WithColor(embedColor)
                                .WithFooter(footer)
                                .WithTitle($"Помощь по утилитам")
                                .AddField($"> `{prefix}банворд <слова> ({prefix}бв <слова>)`",
                                "**Описание:** Добавление слов в вордфильтр. Можно добавить сразу несколько, если писать каждое с новой строки")
                                .AddField($"> `{prefix}банвордудалить <слово> ({prefix}бву <слово>)`",
                                "**Описание:** Удаление слова из вордфильтра. В отличие от команды выше, принимает только одно слово")
                                .AddField($"> `{prefix}замена <заменяющее> <заменяемое> ({prefix}зам <заменяющее> <заменяемое>)`",
                                "**Описание:** Добавление замен для букв/набора букв. Первый параметр заменяется вторым. То есть если ввести `у.зам 0 о`," +
                                "то `0` (ноль) будет заменяться на букву `о`. Если вместо второго параметра ввести `...`, то буква из первого параметра будет удаляться." +
                                "То есть если ввести `у.зам абв ...`, то `абв` будет удаляться и не будет учитываться во время проверки сообщения")
                                .AddField($"> `{prefix}исключение <слова> ({prefix}иск <слова>)`",
                                "**Описание:** Добавление слов в исключения вордфильтра. Можно добавить сразу несколько, если писать каждое с новой строки")
                                .AddField($"> `{prefix}исключениеудалить <слово> ({prefix}иску <слово>)`",
                                "**Описание:** Удаление слова из исключения вордфильтра. В отличие от команды выше, принимает только одно слово")
                                .AddField($"> `{prefix}показатьбанворды ({prefix}пбв)`",
                                "**Описание:** Отображает список банвордов")
                                .AddField($"> `{prefix}показатьзамены ({prefix}пзам)`",
                                "**Описание:** Отображает список замен")
                                .AddField($"> `{prefix}показатьисключения ({prefix}писк)`",
                                "**Описание:** Отображает список исключений")
                                .AddField($"> `{prefix}банвордзадержка <время> ({prefix}бвз <время>)`",
                                "**Описание:** Меняет время удаления уведомления об удалении банворда. Измеряется в *миллисекундах*")
                                .AddField($"> `{prefix}миниатюра <ссылка> ({prefix}мин <ссылка>)`",
                                "**Описание:** Меняет изображение, которое появляется в уведомлении об удалении банворда")
                                .AddField($"> `{prefix}конфигфильтра ({prefix}кф)`",
                                "**Описание:** Отображает текущие настройки вордфильтра")
                                .AddField($"> `{prefix}стеретьбанворды`",
                                "**Описание:** Загружает 1000 последних сообщений с каждого канала и ищет в них банворды, удаляя их.");

                            Context.Channel.SendMessageAsync(embed: embed.Build());
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            
            // Adds a new ban word to the list
            [Command("банворд")]
            [Alias("бв")]
            public async Task Banwords([Remainder] string text)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.банворд", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    string[] words = text.Split("\n");

                    foreach (string word in words)
                    {
                        bool flag = false;
                        foreach (string check in banwords)
                        {
                            if (check == word) { flag = true; break; }
                        }
                        if (!flag) banwords.Add(word);
                    }

                    Context.Channel.SendMessageAsync("Добавлено");

                    SaveWords();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Removes a ban word from the list
            [Command("банвордудалить")]
            [Alias("бву")]
            public async Task Remover(string word)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.банвордудалить", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    banwords.Remove(word);

                    SaveWords();
                    Context.Channel.SendMessageAsync("Удалено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Saves banwords into XML file
            void SaveWords()
            {
                try
                {
                    using (FileStream fs = File.Create(banwordsFolder))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                      "<config>\n" +
                                                                      "</config>\n");
                        fs.Write(info, 0, info.Length);
                        fs.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(banwordsFolder);
                XmlElement root = doc.DocumentElement;

                foreach (string word in banwords)
                {
                    XmlElement line = doc.CreateElement("word");
                    XmlText wordText = doc.CreateTextNode(word);
                    line.AppendChild(wordText);
                    root.AppendChild(line);
                }

                doc.Save(banwordsFolder);
            }

            // Adds a letter replacer (for example: if someone sends "0" instead of "O")
            [Command("замена")]
            [Alias("зам")]
            public async Task Replacer(string toReplace, string replacer)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.замена", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    replacer = replacer.Replace("...", "");
                    foreach (Replacer rpl in replacers)
                    {
                        if (rpl.toReplace == toReplace && rpl.replacer == replacer)
                        {
                            Context.Channel.SendMessageAsync("Такой заменитель уже есть!");
                            return;
                        }
                    }

                    replacers.Add(new Replacer(toReplace, replacer));

                    SaveReplacers();
                    Context.Channel.SendMessageAsync("Добавлено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Removes a letter replacer
            [Command("заменаудалить")]
            [Alias("заму")]
            public async Task ReplacerRemove(string toReplace, string replacer)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.заменаудалить", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    replacer = replacer.Replace("...", "");
                    int i = -1;
                    foreach (Replacer rpl in replacers)
                    {
                        i++;
                        if (rpl.toReplace == toReplace && rpl.replacer == replacer)
                            break;
                    }

                    replacers.RemoveAt(i);

                    SaveReplacers();
                    Context.Channel.SendMessageAsync("Удалено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Saves replacers into XML file
            void SaveReplacers()
            {
                try
                {
                    using (FileStream fs = File.Create(replaceFolder))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                      "<config>\n" +
                                                                      "</config>\n");
                        fs.Write(info, 0, info.Length);
                        fs.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(replaceFolder);
                XmlElement root = doc.DocumentElement;

                foreach (Replacer rp in replacers)
                {
                    XmlElement line = doc.CreateElement("word");
                    XmlElement toReplaceEl = doc.CreateElement("toReplace");
                    XmlElement replacerEl = doc.CreateElement("replacer");
                    XmlText toReplaceText = doc.CreateTextNode(rp.toReplace);
                    XmlText replacerText = doc.CreateTextNode(rp.replacer);
                    toReplaceEl.AppendChild(toReplaceText);
                    replacerEl.AppendChild(replacerText);
                    line.AppendChild(toReplaceEl);
                    line.AppendChild(replacerEl);
                    root.AppendChild(line);
                }

                doc.Save(replaceFolder);
            }

            // Adds an exception word (for example if good word contains a bad word)
            // Example:
            //          Good word: square
            //          Bad word:  are
            // In this case word "square" should be protected by the command below
            [Command("исключение")]
            [Alias("иск")]
            public async Task ExceptionAdd([Remainder] string text)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.исключение", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    string[] words = text.Split("\n");

                    foreach (string word in words)
                    {
                        bool flag = false;
                        foreach (string check in exceptions)
                        {
                            if (check == word) { flag = true; break; }
                        }
                        if (!flag) exceptions.Add(word);
                    }

                    Context.Channel.SendMessageAsync("Добавлено");

                    SaveExceptions();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Deletes an exception word
            [Command("исключениеудалить")]
            [Alias("иску")]
            public async Task ExceptionRemove(string word)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.исключениеудалить", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    exceptions.Remove(word);

                    SaveExceptions();
                    Context.Channel.SendMessageAsync("Удалено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Saces exceptions
            void SaveExceptions()
            {
                try
                {
                    using (FileStream fs = File.Create(exceptionsFolder))
                    {
                        byte[] info = new UTF8Encoding(true).GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                                                                      "<config>\n" +
                                                                      "</config>\n");
                        fs.Write(info, 0, info.Length);
                        fs.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(exceptionsFolder);
                XmlElement root = doc.DocumentElement;

                foreach (string word in exceptions)
                {
                    XmlElement line = doc.CreateElement("word");
                    XmlText exceptionText = doc.CreateTextNode(word);
                    line.AppendChild(exceptionText);
                    root.AppendChild(line);
                }

                doc.Save(exceptionsFolder);
            }

            // Shows all ban words
            [Command("показатьбанворды")]
            [Alias("пбв")]
            public async Task BanwordsShow()
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.показатьбанворды", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    string msg = "";

                    foreach (string word in banwords)
                    {
                        msg += $"{word}\n";
                    }

                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(embedColor)
                        .WithFooter(footer)
                        .WithTitle("Банворды")
                        .WithDescription(msg.Substring(0, msg.Length < 2000 ? msg.Length : 2000));

                    Context.Channel.SendMessageAsync(embed: embed.Build());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Shows all added replacers
            [Command("показатьзамены")]
            [Alias("пзам")]
            public async Task ReplacerShow()
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.показатьзамены", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    string toReplaces = "";
                    string replacerss = "";

                    foreach (Replacer rp in replacers)
                    {
                        toReplaces += rp.toReplace + "\n";
                        replacerss += rp.replacer + "\n";
                    }

                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(embedColor)
                        .WithFooter(footer)
                        .AddField("Что заменяется", toReplaces, true)
                        .AddField("На что заменяется", replacerss, true);

                    Context.Channel.SendMessageAsync(embed: embed.Build());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Shows all exception words
            [Command("показатьисключения")]
            [Alias("писк")]
            public async Task ExceptionsShow()
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.показать исключения", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    string msg = "";

                    foreach (string word in exceptions)
                    {
                        msg += $"{word}\n";
                    }

                    EmbedBuilder embed = new EmbedBuilder();
                    embed.WithColor(embedColor)
                        .WithFooter(footer)
                        .WithTitle("Исключения")
                        .WithDescription(msg.Substring(0, msg.Length < 2000 ? msg.Length : 2000));

                    Context.Channel.SendMessageAsync(embed: embed.Build());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            // Changes the time after which warning messages are deleted 
            [Command("банвордзадержка")]
            [Alias("бвз")]
            public async Task Delayer(int mill)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.банвордзадержка value change", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    millis = mill;

                    SaveFilterConfig();
                    Context.Channel.SendMessageAsync("Изменено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Changes the thumbnail image in the warning message
            [Command("миниатюра")]
            [Alias("мин")]
            public async Task Thumbnail(string link)
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.миниатюра value change", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    thumbnailLink = link;

                    SaveFilterConfig();
                    Context.Channel.SendMessageAsync("Изменено");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Opens Word Filter dashboard
            [Command("конфигфильтра")]
            [Alias("кф")]
            public async Task ConfigShow()
            {
                try
                {
                    if (!IsAdmin(Context.User as SocketGuildUser, "у.конфиг", AccessLevel.Kappa)) return;
                    if (!IsAllowedChannel(Context.Channel.Id)) return;

                    EmbedBuilder embed = GetFilterConfigEmbed();

                    Context.Channel.SendMessageAsync(embed: embed.Build()).Result
                        .AddReactionsAsync(new Emoji[3] { new Emoji("1️⃣"), new Emoji("2️⃣"), new Emoji("🔄") });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            #endregion

            #region AntiSpam
            // AntiSpam help command
            [Command("помощьантиспам")]
            [Alias("па")]
            public async Task HelpAntiSpam()
            {
                try
                {
                    SocketGuild guild = Context.Guild;
                    if (IsAdmin(Context.User as SocketGuildUser, "", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            EmbedBuilder embed = new EmbedBuilder();
                            embed.WithColor(embedColor)
                                .WithFooter(footer)
                                .WithTitle($"Помощь по утилитам")
                                .AddField($"> `{prefix}настройки (н.н)`",
                                "**Описание:** Открывает настройки антиспама, содержащий также ещё 7 команд для изменения настроек.")
                                .AddField($"> `{prefix}аслог`",
                                "**Описание:** Выводит логи, которые есть в консоли бота.")
                                .AddField($"> `{prefix}график`",
                                "**Описание:** Запрашивает графики по отдельным каналам. Графики содержат информацию о работе весов " +
                                "в определённое время.");

                            Context.Channel.SendMessageAsync(embed: embed.Build());
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            // AntiSpam dashboard
            [Command("настройки")]
            [Alias("н")]
            public async Task AntiSpamSettings()
            {
                try
                {
                    if (IsAdmin(Context.User as SocketGuildUser, "н.настройки", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            Context.Channel.SendMessageAsync(embed: GetAntiSpamEmbed(Context.Message, Context.Guild).Build()).Result
                                .AddReactionsAsync(new Emoji[8] { new Emoji("1️⃣"), new Emoji("2️⃣"), new Emoji("3️⃣"),
                            new Emoji("6️⃣"), new Emoji("7️⃣"), new Emoji("◀️"), new Emoji("▶️"), new Emoji("🔄")});
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            // Adds a channel with the desired settings 
            [Command("доб", RunMode = RunMode.Async)]
            public async Task AddSettings()
            {
                try
                {
                    if (IsAdmin(Context.User as SocketGuildUser, "н.доб", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            ulong channelID = 0;
                            AntiSpamBuilder asb = new AntiSpamBuilder();

                            var settingMessage = Context.Channel.SendMessageAsync(embed: CreateEmbed(asb).Build()).Result;

                            while (true)
                            {
                                var message = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (message == null)
                                {
                                    settingMessage.DeleteAsync();
                                    return;
                                }
                                if (message.Content == exitSentense)
                                {
                                    settingMessage.DeleteAsync();
                                    return;
                                }
                                if (message.Content == saveSentense)
                                {
                                    if (channelID != 0)
                                    {
                                        channelsSettings.Add(channelID, new AntiSpam(asb));
                                        SaveAntiSpamChannels();
                                        settingMessage.DeleteAsync();
                                        return;
                                    }
                                }

                                string result = "Результаты:\n";

                                foreach (Match match in Regex.Matches(message.Content, @"^(\d)\s*(\S*)", options: RegexOptions.Multiline))
                                {
                                    switch (match.Groups[1].Value.Replace(" ", ""))
                                    {
                                        case "0":
                                            Regex rg = new Regex(@"^\<\#(\d*)\>");
                                            if (rg.IsMatch(match.Groups[2].Value))
                                            {
                                                try
                                                {
                                                    channelID = Context.Guild.GetTextChannel(ulong.Parse(rg.Matches(match.Groups[2].Value)[0].Groups[1].Value)).Id;
                                                    if (!channelsSettings.ContainsKey(channelID))
                                                    {
                                                        asb.channel = channelID;
                                                        result += $"`{match.Value}` - успех";
                                                    }
                                                    else
                                                    {
                                                        result += $"`{match.Value}` - настройки для указанного канала уже существуют";
                                                    }
                                                }
                                                catch
                                                {
                                                    result += $"`{match.Value}` - неизвестный канал";
                                                }
                                            }
                                            else result += $"`{match.Value}` - ошибка ввода";
                                            break;

                                        case "1":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.multipler))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "2":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.message))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "3":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.attachment))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "4":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.link))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "5":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.linkImage))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "6":
                                            if (int.TryParse(match.Groups[2].Value, out asb.observeTime))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "7":
                                            if (int.TryParse(match.Groups[2].Value, out asb.limit))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "8":
                                            if (int.TryParse(match.Groups[2].Value, out asb.timeout))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "9":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedReduce))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "10":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedLimit))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;

                                        case "11":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedTimeout))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            break;
                                    }
                                    result += $"\n";
                                }

                                settingMessage.ModifyAsync(x => x.Embed = CreateEmbed(asb).Build());
                            }
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Edits the channel settings
            [Command("ред", RunMode = RunMode.Async)]
            public async Task EditSettings()
            {
                try
                {
                    if (IsAdmin(Context.User as SocketGuildUser, "н.ред", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            EmbedBuilder eb = new EmbedBuilder();
                            int option = -1;
                            int index = 1;

                            eb.WithTitle($"Выбери канал для изменения его настроек. Введи \"{exitSentense}\" для отмены");

                            string channelsString = "0) Глобальные настройки\n";
                            foreach (AntiSpam antispam in channelsSettings.Values)
                            {
                                try
                                {
                                    channelsString += $"{index}) {Context.Guild.GetTextChannel(antispam.channel).Name}\n";
                                }
                                catch
                                {
                                    channelsString += $"{index}) {antispam.channel}\n";
                                }
                                index++;
                            }

                            eb.WithDescription($"Каналы: \n{channelsString}");

                            var editingMessage = await Context.Channel.SendMessageAsync(embed: eb.Build());

                            while (true)
                            {
                                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (response == null)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }
                                if (response.Content == exitSentense) return;

                                if (!int.TryParse(response.Content, out option))
                                {
                                    Context.Channel.SendMessageAsync($"Введи именно число, входящее в диапазон 0-{channelsSettings.Count}!");
                                }

                                if (option < 0 || option > channelsSettings.Count) continue;
                                else break;
                            }

                            ulong channelID = 0;
                            AntiSpamBuilder asb = new AntiSpamBuilder();

                            if (option == 0)
                                asb = new AntiSpamBuilder(global);
                            else
                                asb = new AntiSpamBuilder(channelsSettings.ElementAt(option - 1).Value);

                            channelID = asb.channel;

                            editingMessage.ModifyAsync(x => x.Embed = CreateEmbed(asb).Build());

                            while (true)
                            {
                                var message = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (message == null)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }

                                if (message.Content == exitSentense)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }
                                if (message.Content == saveSentense)
                                {
                                    if (channelID == 0)
                                        global = new AntiSpam(asb);
                                    else
                                        channelsSettings[channelID] = new AntiSpam(asb);
                                    SaveAntiSpamChannels();
                                    editingMessage.DeleteAsync();
                                    return;
                                }

                                string result = "Результаты:\n";

                                foreach (Match match in Regex.Matches(message.Content, @"^(\d)\s*(\S*)", options: RegexOptions.Multiline))
                                {
                                    switch (match.Groups[1].Value)
                                    {
                                        case "1":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.multipler))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "2":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.message))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "3":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.attachment))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "4":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.link))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "5":
                                            if (float.TryParse(match.Groups[2].Value.Replace(".", ","), out asb.linkImage))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "6":
                                            if (int.TryParse(match.Groups[2].Value, out asb.observeTime))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "7":
                                            if (int.TryParse(match.Groups[2].Value, out asb.limit))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "8":
                                            if (int.TryParse(match.Groups[2].Value, out asb.timeout))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "9":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedReduce))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "10":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedLimit))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;

                                        case "11":
                                            if (int.TryParse(match.Groups[2].Value, out asb.trustedTimeout))
                                                result += $"`{match.Value}` - успех";
                                            else
                                                result += $"`{match.Value}` - ошибка конвертации";
                                            result += $"\n";
                                            break;
                                    }
                                }

                                editingMessage.ModifyAsync(x => x.Embed = CreateEmbed(asb).Build());
                            }
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Deletes the channel settings
            [Command("уда", RunMode = RunMode.Async)]
            public async Task DeleteSettings()
            {
                try
                {
                    if (IsAdmin(Context.User as SocketGuildUser, "н.уда", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {

                            EmbedBuilder eb = new EmbedBuilder();
                            int option = -1;
                            int index = 1;

                            if (channelsSettings.Count == 0)
                            {
                                eb.WithDescription("Настроек каналов не найдено");
                                Context.Channel.SendMessageAsync(embed: eb.Build());
                                return;
                            }

                            eb.WithTitle($"Выбери канал для удаления его настроек. Введи \"{exitSentense}\" для отмены");

                            string channelsString = "";
                            foreach (AntiSpam antispam in channelsSettings.Values)
                            {
                                try
                                {
                                    channelsString += $"{index}) {Context.Guild.GetTextChannel(antispam.channel).Name}\n";
                                }
                                catch
                                {
                                    channelsString += $"{index}) {antispam.channel}\n";
                                }
                                index++;
                            }

                            eb.WithDescription($"Каналы: \n{channelsString}");

                            var editingMessage = await Context.Channel.SendMessageAsync(embed: eb.Build());

                            while (true)
                            {
                                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (response == null || response.Content == exitSentense)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }
                                if (response.Content == exitSentense) return;

                                if (!int.TryParse(response.Content, out option))
                                {
                                    Context.Channel.SendMessageAsync($"Введи именно число, входящее в диапазон 1-{channelsSettings.Count}!");
                                }

                                if (option < 1 || option > channelsSettings.Count) continue;
                                else break;
                            }

                            ulong channelID = channelsSettings.ElementAt(option - 1).Value.channel;
                            eb.WithTitle($"Введи пинг канала для удаления его настроек. Введи \"{exitSentense}\" для отмены");
                            eb.WithDescription($"<#{channelID}>");
                            editingMessage.ModifyAsync(x => x.Embed = eb.Build());

                            while (true)
                            {
                                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (response == null || response.Content == exitSentense)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }

                                if (response.Content == $"<#{channelID}>")
                                {
                                    channelsSettings.Remove(channelID);
                                    SaveAntiSpamChannels();
                                    eb.WithTitle("Настройки удалены");
                                    editingMessage.ModifyAsync(x => x.Embed = eb.Build());
                                    return;
                                }
                            }
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Duplicates the channel settings for another channel
            [Command("дуб", RunMode = RunMode.Async)]
            public async Task DuplicateSettings()
            {
                try
                {
                    if (IsAdmin(Context.User as SocketGuildUser, "н.дуб", AccessLevel.Kappa))
                        if (IsAllowedChannel(Context.Channel.Id))
                        {
                            EmbedBuilder eb = new EmbedBuilder();
                            int option = -1;
                            int index = 1;

                            eb.WithTitle($"Выбери канал для удаления его настроек. Введи \"{exitSentense}\" для отмены");

                            string channelsString = "0) Глобальные настройки\n";
                            foreach (AntiSpam antispam in channelsSettings.Values)
                            {
                                try
                                {
                                    channelsString += $"{index}) {Context.Guild.GetTextChannel(antispam.channel).Name}\n";
                                }
                                catch
                                {
                                    channelsString += $"{index}) {antispam.channel}\n";
                                }
                                index++;
                            }

                            eb.WithDescription($"Каналы: \n{channelsString}");

                            var editingMessage = await Context.Channel.SendMessageAsync(embed: eb.Build());

                            while (true)
                            {
                                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (response == null || response.Content == exitSentense)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }
                                if (response.Content == exitSentense) return;




                                if (!int.TryParse(response.Content, out option))
                                {
                                    Context.Channel.SendMessageAsync($"Введи именно число, входящее в диапазон 0-{channelsSettings.Count}!");
                                }

                                if (option < 0 || option > channelsSettings.Count) continue;
                                else break;
                            }

                            ulong channelID = 0;
                            eb.WithTitle($"Введи пинг канала для которого будут создаваться настройки. Введи \"{exitSentense}\" для отмены");
                            eb.WithDescription("");
                            editingMessage.ModifyAsync(x => x.Embed = eb.Build());

                            AntiSpamBuilder asb = new AntiSpamBuilder();

                            if (option == 0)
                                asb = new AntiSpamBuilder(global);
                            else
                                asb = new AntiSpamBuilder(channelsSettings.ElementAt(option - 1).Value);

                            while (true)
                            {
                                var response = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (response == null || response.Content == exitSentense)
                                {
                                    editingMessage.DeleteAsync();
                                    return;
                                }

                                Regex rg = new Regex(@"^\<\#(\d*)\>");
                                if (rg.IsMatch(response.Content))
                                {
                                    try
                                    {
                                        channelID = Context.Guild.GetTextChannel(ulong.Parse(rg.Matches(response.Content)[0].Groups[1].Value)).Id;
                                        if (!channelsSettings.ContainsKey(channelID))
                                        {
                                            asb.channel = channelID;
                                            channelsSettings.Add(channelID, new AntiSpam(asb));
                                            SaveAntiSpamChannels();
                                            eb.WithTitle($"Готово! Настройки продублированы для:");
                                            eb.WithDescription($"<#{channelID}>");
                                            editingMessage.ModifyAsync(x => x.Embed = eb.Build());
                                            return;
                                        }
                                    }
                                    catch
                                    {
                                        eb.WithDescription("Канал не найден");
                                        editingMessage.ModifyAsync(x => x.Embed = eb.Build());
                                    }
                                }
                                else
                                {
                                    eb.WithDescription("Канал не найден");
                                    editingMessage.ModifyAsync(x => x.Embed = eb.Build());
                                }
                            }
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }

            // Adds a trusted roles (user with trusted role will be less punished if spamming)
            [Command("дов")]
            public async Task TrustedRoleSet([Remainder] string parameters)
            {
                try
                {
                    if (!Context.Message.Author.IsBot)
                        if (IsAdmin(Context.User as SocketGuildUser, "н.дов", AccessLevel.Kappa))
                            if (IsAllowedChannel(Context.Channel.Id))
                            {
                                Regex regex = new Regex(@"(\+|-)<@&(\d*)>");
                                foreach (Match match in regex.Matches(parameters))
                                {
                                    ulong id = ulong.Parse(match.Groups[2].Value);
                                    switch (match.Groups[1].Value.Replace(" ", ""))
                                    {
                                        case "+":
                                            if (!trustedRoles.Contains(id))
                                                trustedRoles.Add(id);
                                            break;
                                        case "-":
                                            if (trustedRoles.Contains(id))
                                                trustedRoles.Remove(id);
                                            break;
                                    }
                                }

                                string result = "";
                                foreach (ulong id in trustedRoles)
                                {
                                    result += $"<@&{id}> ";
                                }

                                SaveAntiSpamConfig();

                                EmbedBuilder eb = new EmbedBuilder();
                                eb.WithTitle("Текущий список")
                                    .WithDescription(result);

                                await Context.Channel.SendMessageAsync(embed: eb.Build());
                            }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Adds media extensions
            [Command("медиа")]
            public async Task MediaTypesSet([Remainder] string parameters)
            {
                try
                {
                    if (!Context.Message.Author.IsBot)
                        if (IsAdmin(Context.User as SocketGuildUser, "н.медиа", AccessLevel.Kappa))
                            if (IsAllowedChannel(Context.Channel.Id))
                            {
                                Regex regex = new Regex(@"(\+|-)(\w*)");
                                foreach (Match match in regex.Matches(parameters))
                                {
                                    switch (match.Groups[1].Value.Replace(" ", ""))
                                    {
                                        case "+":
                                            if (!mediaFileTypes.Contains(match.Groups[2].Value))
                                                mediaFileTypes.Add(match.Groups[2].Value);
                                            break;
                                        case "-":
                                            if (mediaFileTypes.Contains(match.Groups[2].Value))
                                                mediaFileTypes.Remove(match.Groups[2].Value);
                                            break;
                                    }
                                }

                                string result = "";
                                foreach (string type in mediaFileTypes)
                                {
                                    result += $"{type}|";
                                }
                                result = Regex.Replace(result, @"\|$", "");
                                mediaFileTypesString = result;

                                SaveAntiSpamConfig();

                                EmbedBuilder eb = new EmbedBuilder();
                                eb.WithTitle("Текущий список")
                                    .WithDescription(result);

                                await Context.Channel.SendMessageAsync(embed: eb.Build());
                            }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Changes log channel
            [Command("лог")]
            public async Task LogChannelSet(SocketChannel channel)
            {
                try
                {
                    if (!Context.Message.Author.IsBot)
                        if (IsAdmin(Context.User as SocketGuildUser, "н.лог", AccessLevel.Kappa))
                            if (IsAllowedChannel(Context.Channel.Id))
                            {
                                loggerChannel = channel.Id;

                                SaveAntiSpamConfig();

                                EmbedBuilder eb = new EmbedBuilder();
                                eb.WithTitle("Канал для логов установлен")
                                    .WithDescription($"<#{channel.Id}>");

                                await Context.Channel.SendMessageAsync(embed: eb.Build());

                            }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Shows console logs in Discord embed
            [Command("аслог")]
            public async Task ShowLogs()
            {
                try
                {
                    if (!Context.Message.Author.IsBot)
                        if (IsAdmin(Context.User as SocketGuildUser, "н.аслог", AccessLevel.Kappa))
                            if (IsAllowedChannel(Context.Channel.Id))
                            {
                                EmbedBuilder eb = new EmbedBuilder();
                                eb.WithTitle("Лог (недавние сообщения отображаются в строчках выше)")
                                    .WithDescription(log);

                                await Context.Channel.SendMessageAsync(embed: eb.Build());
                            }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            // Builds a chart of the average message send rate in a specific channel
            [Command("график", RunMode = RunMode.Async)]
            public async Task CreateDiagrams()
            {
                try
                {
                    if (!Context.Message.Author.IsBot)
                        if (IsAdmin(Context.User as SocketGuildUser, "н.график", AccessLevel.Kappa))
                            if (IsAllowedChannel(Context.Channel.Id))
                            {
                                string[] files = Directory.GetFiles(telemetryFolder);
                                EmbedBuilder eb = new EmbedBuilder().WithTitle("Файлы");
                                string fullList = "Введи цифру, график за какой день нужно отобразить. Если нужно отменить выполнение " +
                                    $"команды, введи {exitSentense}\"\"\n\n";

                                for (int i = 1; i <= files.Length; i++)
                                {
                                    fullList += $"{i}) {files[i - 1].Replace(".xml", "").Replace(telemetryFolder, "")}\n";
                                }
                                eb.WithDescription(fullList);
                                var embedded = await Context.Channel.SendMessageAsync(embed: eb.Build());

                                int option = 0;
                                SocketMessage message = null;
                                while (true)
                                {
                                    message = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                    if (message.Content.Contains(exitSentense))
                                    {
                                        Context.Channel.SendMessageAsync("Отменяю выполнение команды");
                                        return;
                                    }

                                    if (int.TryParse(message.Content, out option))
                                    {
                                        if (option >= 1 && option <= files.Length)
                                        {
                                            option--;
                                            break;
                                        }
                                        else { Context.Channel.SendMessageAsync("Такого файла я не нашла!"); continue; }
                                    }
                                    else { Context.Channel.SendMessageAsync("Нужно ввести число!"); continue; }
                                }

                                Dictionary<string, List<TelemetryMessages>> savedMessages = new Dictionary<string, List<TelemetryMessages>>();
                                #region file opening
                                XmlDocument reportsFile = new XmlDocument();
                                reportsFile.Load($@"{files[option]}");
                                XmlElement root = reportsFile.DocumentElement;

                                foreach (XmlNode node in root)
                                {
                                    string channelName = node.ChildNodes[1].InnerText;

                                    if (!savedMessages.ContainsKey(channelName))
                                        savedMessages.Add(channelName, new List<TelemetryMessages>());

                                    savedMessages[channelName].Add(
                                        new TelemetryMessages(float.Parse(node.ChildNodes[2].InnerText),
                                        ulong.Parse(node.ChildNodes[0].InnerText),
                                        DateTimeOffset.Parse(node.ChildNodes[3].InnerText).AddHours(3)));
                                }
                                #endregion file opening

                                eb = new EmbedBuilder().WithTitle("Каналы");
                                string channelList = "Океюшки, теперь введи через пробел цифры тех каналов, по которым нужно построить графики\n\n";

                                for (int i = 0; i < savedMessages.Count; i++)
                                {
                                    channelList += $"{i + 1}) {savedMessages.ElementAt(i).Key} ({savedMessages.ElementAt(i).Value.Count})\n";
                                }
                                eb.WithDescription(channelList);
                                await embedded.ModifyAsync(x => x.Embed = eb.Build());

                                message = await NextMessageAsync(timeout: TimeSpan.FromSeconds(120));

                                if (message.Content.Contains(exitSentense))
                                {
                                    Context.Channel.SendMessageAsync("Отменяю выполнение команды");
                                    return;
                                }

                                string[] options = message.Content.Split(" ");

                                foreach (string opt in options)
                                    try
                                    {
                                        CreateReport(savedMessages.ElementAt(int.Parse(opt) - 1).Value, savedMessages.ElementAt(int.Parse(opt) - 1).Key, files[option].Replace(".xml", "").Replace(telemetryFolder, ""));
                                        await Context.Channel.SendFileAsync(reportImage);
                                    }
                                    catch (Exception) { }
                                embedded.DeleteAsync();
                            }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
            }

            // Embed builder of channel settings
            EmbedBuilder CreateEmbed(AntiSpamBuilder asb)
            {
                EmbedBuilder eb = new EmbedBuilder()
                    .WithTitle("Редактирование настроек")
                    .WithFooter(footer);

                string desc = "Для изменения настройки введи \"<цифра настройки> <значение настройки>\", каждое новое значение настройки через новую строку. Например:\n" +
                    "0 <#477927077098815488>\n" +
                    "1 3,5\n" +
                    "7 20\n" +
                    "9 50\n\n" +
                    "Пояснение для скобок:\n" +
                    "`<#123>` - пинг канала - <#477927077098815488> - можно указать только если создаётся, либо дублируется, новый набор настроек\n" +
                    "`1` - целое число - 1, 2, и т.д.\n" +
                    "`1,0` - число с дробной частью - 2,5; 4,123; и т.д.\n" +
                    "`0-100%` - диапазон целых чисел для процентов - 3, 14, 79 - пишется без знака \"%\"\n\n" +
                    $"Для сохранения введи \"{saveSentense}\", для отмены - \"{exitSentense}\"\n";
                desc += $"0) Канал (`<#123>`): <#{asb.channel}>\n";
                desc += $"1) Множитель (1,0): {asb.multipler}\n";
                desc += $"2) Вес сообщения (1,0): {asb.message}\n";
                desc += $"3) Вес прикреплённого файла (1,0): {asb.attachment}\n";
                desc += $"4) Вес ссылки (1,0): {asb.link}\n";
                desc += $"5) Вес ссылки с картинкой (1,0): {asb.linkImage}\n";
                desc += $"6) Время отслеживания (1): {asb.observeTime}\n";
                desc += $"7) Лимит (1): {asb.limit}\n";
                desc += $"8) Таймаут (1): {asb.timeout}\n";
                desc += $"9) Сокращение веса доверенной роли (0-100%): {asb.trustedReduce}\n";
                desc += $"10) Лимит доверенной роли (1): {asb.trustedLimit}\n";
                desc += $"11) Таймаут доверенной роли (1): {asb.trustedTimeout}\n";

                eb.WithDescription(desc);

                return eb;
            }

            #endregion
        }

        // Access levels
        public enum AccessLevel
        {
            All,
            Kappa,
            Gods,
            Creator
        }
        // Checks if the user executing command can do it
        public static bool IsAdmin(SocketGuildUser user, string context, AccessLevel level = AccessLevel.All)
        {
            if (user == null) return false;
            bool access = false;
            foreach (SocketRole role in user.Roles)
            {
                switch (level)
                {
                    case AccessLevel.All: access = true; break;
                    case AccessLevel.Kappa:
                        if (role.Id == kappa || role.Id == kappaTest) access = true;
                        if (role.Id == gods || role.Id == godsTest) access = true;
                        break;
                    case AccessLevel.Gods: if (role.Id == gods || role.Id == godsTest) access = true; break;
                    case AccessLevel.Creator: if (user.Id == creator) access = true; break;
                    default: access = false; break;
                }
            }

            if (context != "")
                Console.WriteLine($"[{DateTime.Now}]{user} {context} access - {access}");

            return access;
        }
        // Checks if the user executing command in proper channel
        public static bool IsAllowedChannel(ulong channel)
        {
            foreach (ulong id in allowedChannels)
                if (channel == id) return true;

            return false;
        }
    }
}
