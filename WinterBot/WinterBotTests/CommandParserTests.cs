﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winter;

namespace WinterBotTests
{
    [TestClass]
    public class CommandParserTests
    {
        WinterBot m_bot;

        [TestInitialize]
        public void Initialize()
        {
            m_bot = new WinterBot(new Options(), "testdata", "username", "oauth:123");
        }

        [TestMethod]
        public void ParseCommand()
        {
            // Ensure it works with preceeding/trailing spaces
            Args cmd = "  -flag -int=123 -ul=mod UserName 1234 the remainder ".ParseArguments(m_bot);
            ParseCommandWorker(cmd);

            // Ensure it works without preceeding/trailing spaces
            cmd = "-flag -int=123 -ul=mod UserName 1234 the remainder".ParseArguments(m_bot);
            ParseCommandWorker(cmd);
        }

        private static void ParseCommandWorker(Args cmd)
        {
            Assert.IsNotNull(cmd);

            Assert.IsTrue(cmd.GetFlag("FlAg"));
            Assert.IsNull(cmd.Error);

            Assert.IsFalse(cmd.GetFlag("notpresent"));
            Assert.IsNull(cmd.Error);

            int intValue;
            Assert.IsTrue(cmd.GetIntFlag("int", out intValue));
            Assert.AreEqual(123, intValue);
            Assert.IsNull(cmd.Error);

            Assert.AreEqual(123, cmd.GetIntFlag("int", -1, true));
            Assert.IsNull(cmd.Error);

            AccessLevel accessValue;
            Assert.IsTrue(cmd.GetAccessFlag("ul", out accessValue));
            Assert.AreEqual(AccessLevel.Mod, accessValue);
            Assert.IsNull(cmd.Error);

            Assert.AreEqual(AccessLevel.Mod, cmd.GetAccessFlag("ul", AccessLevel.Normal, true));
            Assert.IsNull(cmd.Error);

            TwitchUser user = cmd.GetUser();
            Assert.IsNotNull(user);
            Assert.IsNull(cmd.Error);
            Assert.AreEqual("username", user.Name);

            intValue = cmd.GetInt();
            Assert.IsNull(cmd.Error);
            Assert.AreEqual(1234, intValue);

            string remainder = cmd.GetString();
            Assert.IsNull(cmd.Error);
            Assert.IsNotNull(remainder);
            Assert.AreEqual("the remainder", remainder);
        }

        [TestMethod]
        public void TestEmptyCommand()
        {
            Args cmd = GetEmptyCmd();
            Assert.IsNotNull(cmd);

            Assert.AreEqual("", cmd.GetString());
        }

        [TestMethod]
        public void EmptyCommandUserTest()
        {
            Args cmd = GetEmptyCmd();
            Assert.IsNotNull(cmd);

            Assert.IsNull(cmd.GetUser());
        }

        [TestMethod]
        public void TestOneMethod()
        {
            Args cmd = "  one  two 3 ".ParseArguments(m_bot);
            Assert.IsNotNull(cmd);

            Assert.AreEqual("one", cmd.GetOneWord());
            Assert.IsNull(cmd.Error);

            Assert.AreEqual("two", cmd.GetOneWord());
            Assert.IsNull(cmd.Error);

            Assert.AreEqual("3", cmd.GetOneWord());
            Assert.IsNull(cmd.Error);
        }


        [TestMethod]
        public void NegativeCommandValuesTest()
        {
            Args cmd = GetEmptyCmd();
            Assert.IsNotNull(cmd);

            Assert.IsFalse(cmd.GetFlag("notpresent"));
            Assert.IsNull(cmd.Error);

            cmd = GetEmptyCmd();
            int value;
            Assert.IsFalse(cmd.GetInt(out value));
            Assert.IsNotNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.IsNull(cmd.GetUser());
            Assert.IsNotNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.IsNull(cmd.GetOneWord());
            Assert.IsNotNull(cmd.Error);

        }

        [TestMethod]
        public void AccessLevelNegativeTest()
        {
            Args cmd = GetEmptyCmd();
            AccessLevel accessValue;
            Assert.IsFalse(cmd.GetAccessFlag("ul", out accessValue));
            Assert.AreEqual(AccessLevel.Mod, accessValue);
            Assert.IsNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.AreEqual(AccessLevel.Normal, cmd.GetAccessFlag("ul", AccessLevel.Normal, true));
            Assert.IsNotNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.AreEqual(AccessLevel.Normal, cmd.GetAccessFlag("ul", AccessLevel.Normal, false));
            Assert.IsNull(cmd.Error);
        }


        [TestMethod]
        public void IntFlagNegativeTest()
        {
            Args cmd = GetEmptyCmd();
            int intValue;
            Assert.IsFalse(cmd.GetIntFlag("int", out intValue, true));
            Assert.IsNotNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.IsFalse(cmd.GetIntFlag("int", out intValue, false));
            Assert.IsNull(cmd.Error);


            cmd = GetEmptyCmd();
            Assert.AreEqual(-1, cmd.GetIntFlag("int", -1, true));
            Assert.IsNotNull(cmd.Error);

            cmd = GetEmptyCmd();
            Assert.AreEqual(-1, cmd.GetIntFlag("int", -1, false));
            Assert.IsNull(cmd.Error);
        }

        private Args GetEmptyCmd()
        {
            return "            ".ParseArguments(m_bot);
        }

        [TestMethod]
        public void UserCommandTest()
        {
            Args cmd = "  -ul=reg !SomeTestCommand Some text to go with it. ".ParseArguments(m_bot);
            Assert.IsNotNull(cmd);

            AccessLevel level;
            Assert.IsTrue(cmd.GetAccessFlag("ul", out level, true));
            Assert.IsNull(cmd.Error);
            Assert.AreEqual(AccessLevel.Regular, level);

            string cmdName = cmd.GetOneWord();
            Assert.IsNull(cmd.Error);
            Assert.AreEqual("!SomeTestCommand", cmdName);

            string cmdText = cmd.GetString();
            Assert.IsNull(cmd.Error);
            Assert.AreEqual("Some text to go with it.", cmdText);
        }
    }
}
