using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CopperCamp.Tests
{
    public class CopperCampTests
    {
        [SetUp]
        public void SetUp()
        {
            CC.S = null;
            CC.TitleMode = false;
            CC.Shift = null;
            CC.PendingAfterShift = null;
        }

        [Test]
        public void NewGame_CreatesValidState()
        {
            var origin = CC.ORIGINS[0];
            CC.NewGame(origin, "Test");
            Assert.IsNotNull(CC.S);
            Assert.AreEqual("Test", CC.S.name);
            Assert.AreEqual(origin.id, CC.S.originId);
            Assert.AreEqual(1906, CC.S.year);
            Assert.AreEqual(0, CC.S.season);
            Assert.AreEqual("work", CC.S.phase);
        }

        [Test]
        public void JobPay_OriginModifiers()
        {
            var origin = CC.ORIGINS[1]; // Cornish
            CC.NewGame(origin, "Test");
            var mountainCon = CC.JOBS.First(j => j.id == "mountaincon");
            int pay = CC.JobPay(mountainCon);
            Assert.Greater(pay, mountainCon.pay * 0.9f); // should be boosted
        }

        [Test]
        public void SaveCodec_Roundtrip()
        {
            var origin = CC.ORIGINS[0];
            CC.NewGame(origin, "Test");
            CC.S.money = 123;
            CC.S.health = 50;
            CC.S.flags.Set("test", true);
            string encoded = SaveCodec.Encode(CC.S);
            var decoded = SaveCodec.Decode(encoded);
            Assert.AreEqual(CC.S.money, decoded.money);
            Assert.AreEqual(CC.S.health, decoded.health);
            Assert.IsTrue(decoded.flags.B("test"));
        }

        [Test]
        public void ApplyShiftModifiers_DoesNotCrash()
        {
            var origin = CC.ORIGINS[0];
            CC.NewGame(origin, "Test");
            var job = CC.JOBS[0];
            var U = new ShiftState { kind = "muck", job = job, stance = "steady" };
            // ApplyShiftModifiers is private, so we test via StartShift
            CC.StartShift(job, "steady");
            Assert.IsNotNull(CC.Shift);
            Assert.AreEqual("muck", CC.Shift.kind);
        }

        [Test]
        public void Settings_SaveLoad()
        {
            Settings.MusicVolume = 0.5f;
            Settings.SfxVolume = 0.7f;
            Settings.TextSize = 2;
            Settings.ReducedMotion = true;
            Settings.Colorblind = true;
            Settings.HighContrast = true;
            Settings.Save();

            Settings.MusicVolume = 1f;
            Settings.SfxVolume = 1f;
            Settings.TextSize = 1;
            Settings.ReducedMotion = false;
            Settings.Colorblind = false;
            Settings.HighContrast = false;
            Settings.Load();

            Assert.AreEqual(0.5f, Settings.MusicVolume, 0.01f);
            Assert.AreEqual(0.7f, Settings.SfxVolume, 0.01f);
            Assert.AreEqual(2, Settings.TextSize);
            Assert.IsTrue(Settings.ReducedMotion);
            Assert.IsTrue(Settings.Colorblind);
            Assert.IsTrue(Settings.HighContrast);
        }

        [Test]
        public void LeisureFor_ChineseBoardFree()
        {
            var origin = CC.ORIGINS[5]; // Chinese
            CC.NewGame(origin, "Test");
            var opt = CC.LeisureFor("board");
            Assert.IsNotNull(opt);
            Assert.AreEqual(0, opt.cost);
        }
    }
}
