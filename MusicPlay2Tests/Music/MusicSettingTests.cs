using Microsoft.VisualStudio.TestTools.UnitTesting;
using MusicPlay2.Music;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicPlay2.Music.Tests {
    [TestClass()]
    public class MusicSettingTests {
        [TestMethod()]
        public void WriteTest() {
            MusicSetting musicSetting = new MusicSetting();
            List<object> list = new List<object> {
                "test1文本",
                9000,
                300,
                "test2的文本"
            };
            musicSetting.Write(800, "SIIS", list);
            musicSetting.Save();
        }

        [TestMethod()]
        public void ReadTest() {
            MusicSetting musicSetting = new MusicSetting();
            Assert.AreEqual(true, musicSetting.MovePositionById(800));
            (uint id, List<object> list) = musicSetting.Read("SIIS");
            Assert.AreEqual(800U, id);
            Assert.AreEqual("test1文本", list[0]);
            Assert.AreEqual(9000, list[1]);
            Assert.AreEqual(300, list[2]);
            Assert.AreEqual("test2的文本", list[3]);
        }
    }
}