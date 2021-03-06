﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Winter
{
    public class TwitchUsers
    {
        static Regex s_validUsername = new Regex("^[a-zA-Z][a-zA-Z0-9_]*$");
        Dictionary<string, TwitchUser> m_users;
        HashSet<TwitchUser> m_moderators;
        object m_sync = new object();
        public WinterBot Bot { get; private set; }

        internal HashSet<TwitchUser> ModeratorSet
        {
            get
            {
                if (m_moderators == null)
                    m_moderators = new HashSet<TwitchUser>();

                return m_moderators;
            }

            set
            {
                m_moderators = value;
            }
        }

        public IEnumerable<TwitchUser> Moderators
        {
            get
            {
                return m_moderators;
            }
        }

        public TwitchUsers(WinterBot bot)
        {
            m_users = new Dictionary<string, TwitchUser>();
            Bot = bot;

            var streamer = GetUser(bot.Channel);
            streamer.IsStreamer = true;
            streamer.IsModerator = true;
        }


        public TwitchUsers(string channel)
        {
            m_users = new Dictionary<string, TwitchUser>();

            var streamer = GetUser(channel);
            streamer.IsStreamer = true;
            streamer.IsModerator = true;
        }


        public TwitchUser GetUser(string username, bool create=true)
        {
            username = username.ToLower();
            TwitchUser user;

            lock (m_sync)
            {
                if (!m_users.TryGetValue(username, out user) && create)
                {
                    user = new TwitchUser(this, username);
                    m_users[username] = user;
                }
            }

            return user;
        }


        public static bool IsValidUserName(string user)
        {
            if (user == null)
                return false;

            return s_validUsername.IsMatch(user);
        }
    }

    public class TwitchUser
    {
        private TwitchUsers m_data;
        public string Color { get; internal set; }
        public int[] IconSet { get; internal set; }

        public string Name { get; internal set; }

        public bool IsNormalUser
        {
            get
            {
                return !IsSubscriber && !IsModerator && !IsRegular && !IsStreamer;
            }
        }

        public AccessLevel Access
        {
            get
            {
                if (IsStreamer)
                    return AccessLevel.Streamer;
                if (IsModerator)
                    return AccessLevel.Mod;
                if (IsSubscriber)
                    return AccessLevel.Subscriber;
                if (IsRegular)
                    return AccessLevel.Regular;

                return AccessLevel.Normal;
            }
        }

        public bool IsStreamer { get; internal set; }

        public bool IsModerator { get; internal set; }

        public bool IsSubscriber { get; internal set; }

        public bool IsRegular
        {
            get
            {
                var bot = m_data.Bot;
                if (bot == null)
                    return false;

                return m_data.Bot.IsRegular(this);
            }
            internal set
            {
                var bot = m_data.Bot;
                if (bot != null && bot.IsRegular(this) != value)
                {
                    if (value)
                        bot.AddRegular(this);
                    else
                        bot.RemoveRegular(this);
                }
            }
        }

        public TwitchUser(TwitchUsers data, string name)
        {
            Name = name.ToLower();
            m_data = data;
            Debug.Assert(data != null);
        }

        public override string ToString()
        {
            return Name;
        }

        public void EnsureIconsDownloaded()
        {
            var sets = IconSet;
            if (sets != null)
            {
                foreach (int i in sets)
                    TwitchHttp.Instance.EnsureEmoticonsLoaded(i);
            }
            else
            {
                TwitchHttp.Instance.EnsureEmoticonsLoaded();
            }
        }
    }
}
