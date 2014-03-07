﻿using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinterBot
{
    public enum AccessLevel
    {
        Normal,
        Regular,
        Subscriber,
        Mod,
        Streamer
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class BotCommandAttribute : Attribute
    {
        public string[] Commands { get; set; }
        public AccessLevel AccessRequired { get; set; }

        public BotCommandAttribute(AccessLevel accessRequired, params string[] commands)
        {
            Commands = commands;
            AccessRequired = accessRequired;
        }
    }

    public class BuiltInCommands
    {
        public BuiltInCommands()
        {
        }

        [BotCommand(AccessLevel.Mod, "addreg", "addregular")]
        public void AddRegular(WinterBot sender, TwitchUser user, string cmd, string value)
        {
            SetRegular(sender, cmd, value, true);
        }

        [BotCommand(AccessLevel.Mod, "delreg", "delregular", "remreg", "remregular")]
        public void RemoveRegular(WinterBot sender, TwitchUser user, string cmd, string value)
        {
            SetRegular(sender, cmd, value, false);
        }

        private void SetRegular(WinterBot sender, string cmd, string value, bool regular)
        {
            value = value.Trim().ToLower();

            var userData = sender.UserData;
            if (!userData.IsValidUserName(value))
            {
                sender.WriteDiagnostic(DiagnosticLevel.Notify, "{0}: Invalid username '{1}.", cmd, value);
                return;
            }

            var reg = userData.GetUser(value);
            reg.IsRegular = regular;

            if (regular)
                sender.SendMessage("{0} added to regular list.", value);
            else
                sender.SendMessage("{0} removed from regular list.", value);
        }
    }

    public class TimeoutController
    {
        private WinterBot m_winterBot;
        HashSet<string> m_permit = new HashSet<string>();
        HashSet<string> m_allowedUrls = new HashSet<string>();
        HashSet<string> m_urlExtensions = new HashSet<string>();
        private HashSet<string> m_defaultImageSet;
        private Dictionary<int, HashSet<string>> m_imageSets;
        Regex m_url = new Regex(@"([\w-]+\.)+([\w-]+)(/[\w- ./?%&=]*)?", RegexOptions.IgnoreCase);
        Regex m_banUrlRegex = new Regex(@"(slutty)|(naked)-[a-zA-Z0-9]+\.com", RegexOptions.IgnoreCase);

        public TimeoutController(WinterBot bot)
        {
            LoadExtensions();
            m_winterBot = bot;
            m_winterBot.MessageReceived += CheckMessage;
        }

        [BotCommand(AccessLevel.Mod, "permit")]
        public void Permit(TwitchUser user, string cmd, string value)
        {
            value = value.Trim().ToLower();

            var userData = m_winterBot.UserData;
            if (!userData.IsValidUserName(value))
            {
                m_winterBot.WriteDiagnostic(DiagnosticLevel.Notify, "{0}: Invalid username '{1}.", cmd, value);
                return;
            }

            m_permit.Add(value);
            m_winterBot.SendMessage("{0} -> {1} has been granted permission to post a single link.", user.Name, value);
        }


        public void CheckMessage(WinterBot sender, TwitchUser user, string text)
        {
            if (user.IsRegular || user.IsSubscriber || user.IsModerator)
                return;

            if (HasSpecialCharacter(text))
            {
                m_winterBot.SendMessage(string.Format("{0}: Sorry, no special characters allowed to keep the dongers to a minimum. (This is not a timeout.)", user.Name));
                user.ClearChat();
                return;
            }

            if (TooManyCaps(text))
            {
                m_winterBot.SendMessage(string.Format("{0}: Sorry, please don't spam caps. (This is not a timeout.)", user.Name));
                user.ClearChat();
                return;
            }

            string url;
            if (HasUrl(text, out url))
            {
                text = text.ToLower();
                url = url.ToLower();
                if (!m_allowedUrls.Contains(url) || (url.Contains("teamliquid") && (text.Contains("userfiles") || text.Contains("image") || text.Contains("profile"))))
                {
                    if (m_banUrlRegex.IsMatch(url) || url.Contains("codes4free.net") || url.Contains("vine4you.com") || url.Contains("prizescode.net"))
                    {
                        m_winterBot.SendMessage(string.Format("{0}: Banned.", user.Name));
                        user.Ban();
                    }
                    else
                    {
                        m_winterBot.SendMessage(string.Format("{0}: Only subscribers are allowed to post links. (This is not a timeout.)", user.Name));
                        user.ClearChat();
                    }

                    return;
                }
            }
        }

        private bool TooManyEmotes(TwitchUser user, string message, out string offender)
        {
            int count = 0;

            offender = message;
            if (m_defaultImageSet != null)
            {
                foreach (string item in m_defaultImageSet)
                {
                    count += CountEmote(message, item);
                    if (count > 3)
                        return true;
                }
            }

            int[] userSets = user.IconSet;
            if (userSets != null && m_imageSets != null)
            {
                foreach (int setId in userSets)
                {
                    HashSet<string> imageSet;
                    if (!m_imageSets.TryGetValue(setId, out imageSet))
                        continue;

                    foreach (string item in imageSet)
                    {
                        count += CountEmote(message, item);
                        if (count > 3)
                            return true;
                    }
                }
            }

            offender = null;
            return false;
        }

        private int CountEmote(string message, string item)
        {
            int count = 0;
            int i = message.IndexOf(item);
            while (i != -1)
            {
                count++;
                i = message.IndexOf(item, i + 1);
            }

            return count;
        }

        private bool TooManyCaps(string message)
        {
            int upper = 0;
            int lower = 0;

            foreach (char c in message)
            {
                if ('a' <= c && c <= 'z')
                    lower++;
                else if ('A' <= c && c <= 'Z')
                    upper++;
            }

            int total = lower + upper;
            if (total <= 15)
                return false;


            int percent = 100 * upper / total;
            if (percent < 70)
                return false;

            return true;
        }


        static bool HasSpecialCharacter(string str)
        {
            for (int i = 0; i < str.Length; ++i)
                if (!Allowed(str[i]))
                    return true;

            return false;
        }

        static bool Allowed(char c)
        {
            if (c < 255)
                return true;

            // punctuation block
            if (0x2010 <= c && c <= 0x2049)
                return true;

            return c == '♥' || c == '…' || c == '€' || IsKoreanCharacter(c);
        }

        static bool IsKoreanCharacter(char c)
        {
            return (0xac00 <= c && c <= 0xd7af) ||
                (0x1100 <= c && c <= 0x11ff) ||
                (0x3130 <= c && c <= 0x318f) ||
                (0x3200 <= c && c <= 0x32ff) ||
                (0xa960 <= c && c <= 0xa97f) ||
                (0xd7b0 <= c && c <= 0xd7ff) ||
                (0xff00 <= c && c <= 0xffef);
        }

        bool HasUrl(string str, out string url)
        {
            url = null;
            var match = m_url.Match(str);
            if (!match.Success)
                return false;

            var groups = match.Groups;
            if (!m_urlExtensions.Contains(groups[groups.Count - 2].Value))
                return false;

            url = groups[1].Value + groups[2].Value;
            return true;
        }

        void LoadExtensions()
        {
            var exts = File.ReadAllLines(@"extensions.txt");
            m_urlExtensions = new HashSet<string>(exts);

            var allowed = File.ReadAllLines(@"whitelist_urls.txt");
            m_allowedUrls = new HashSet<string>(allowed);
        }


        private void LoadEmoticons(object state)
        {
            var req = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(@"https://api.twitch.tv/kraken/chat/emoticons");
            req.UserAgent = "Question Grabber Bot/0.0.0.1";
            var response = req.GetResponse();
            var fromStream = response.GetResponseStream();

            StreamReader reader = new StreamReader(fromStream);
            string data = reader.ReadToEnd();

            TwitchEmoticonResponse emotes = JsonConvert.DeserializeObject<TwitchEmoticonResponse>(data);

            HashSet<string> defaultSet = new HashSet<string>();
            Dictionary<int, HashSet<string>> imageSets = new Dictionary<int, HashSet<string>>();

            foreach (var emote in emotes.emoticons)
            {
                foreach (var image in emote.images)
                {
                    if (image.emoticon_set == null)
                    {
                        defaultSet.Add(emote.regex);
                    }
                    else
                    {
                        int setId = (int)image.emoticon_set;
                        HashSet<string> set;
                        if (!imageSets.TryGetValue(setId, out set))
                            imageSets[setId] = set = new HashSet<string>();

                        set.Add(emote.regex);
                    }
                }
            }

            m_defaultImageSet = defaultSet;
            m_imageSets = imageSets;
        }
    }


    // TODO: User text commands
    // TODO: Interval message commands
}