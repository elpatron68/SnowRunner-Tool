using System;
using System.IO;

namespace SnowRunner_Tool.Tests
{
    [Collection(SerilogCollection.Name)]
    public class CheatGameTests : IDisposable
    {
        private readonly string _tempDir;
        private const string Extension = "cfg";

        public CheatGameTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SRT-Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for temp fixtures.
            }
        }

        private static string MinimalSaveJson(string slotName, int money, int experience)
        {
            return "{\"" + slotName + "\":{\"persistentProfileData\":{\"money\":" + money + ",\"experience\":" + experience + "},\"other\":{\"money\":99}}}";
        }

        private string WriteSlotFile(int slot, string slotName, int money, int experience)
        {
            string fileName = slot == 1
                ? "CompleteSave." + Extension
                : "CompleteSave" + (slot - 1) + "." + Extension;
            string path = Path.Combine(_tempDir, fileName);
            File.WriteAllText(path, MinimalSaveJson(slotName, money, experience));
            return path;
        }

        private string Slot1Path => Path.Combine(_tempDir, "CompleteSave." + Extension);

        [Theory]
        [InlineData(1, "CompleteSave", 1000)]
        [InlineData(2, "CompleteSave1", 2000)]
        [InlineData(3, "CompleteSave2", 3000)]
        [InlineData(4, "CompleteSave3", 4000)]
        public void GetMoney_ReadsCorrectSlotFile(int slot, string slotName, int money)
        {
            WriteSlotFile(slot, slotName, money, experience: 50);

            string result = CheatGame.GetMoney(Slot1Path, slot, Extension);

            Assert.Equal(money.ToString(), result);
        }

        [Fact]
        public void GetMoney_MissingFile_ReturnsNa()
        {
            string result = CheatGame.GetMoney(Slot1Path, 1, Extension);
            Assert.Equal("n/a", result);
        }

        [Fact]
        public void SaveMoney_UpdatesOnlyProfileMoneyInSlot2()
        {
            WriteSlotFile(2, "CompleteSave1", 2000, experience: 50);

            bool ok = CheatGame.SaveMoney(Slot1Path, "555", 2, Extension);

            Assert.True(ok);
            string content = File.ReadAllText(Path.Combine(_tempDir, "CompleteSave1." + Extension));
            Assert.Contains("\"money\":555", content);
            Assert.Contains("\"money\":99", content);
            Assert.DoesNotContain("\"money\":2000", content);
        }

        [Theory]
        [InlineData(1, "CompleteSave", 11)]
        [InlineData(3, "CompleteSave2", 33)]
        public void GetXp_ReadsCorrectSlotFile(int slot, string slotName, int xp)
        {
            WriteSlotFile(slot, slotName, money: 1, experience: xp);

            string result = CheatGame.GetXp(Slot1Path, slot, Extension);

            Assert.Equal(xp.ToString(), result);
        }

        [Fact]
        public void SaveXp_UpdatesExperienceInSlot1()
        {
            WriteSlotFile(1, "CompleteSave", money: 10, experience: 20);

            bool ok = CheatGame.SaveXp(_tempDir, "999", 1, Extension);

            Assert.True(ok);
            string content = File.ReadAllText(Slot1Path);
            Assert.Contains("\"experience\":999", content);
            Assert.Contains("\"money\":10", content);
        }

        [Fact]
        public void SaveXp_MissingFile_ReturnsFalse()
        {
            Assert.False(CheatGame.SaveXp(_tempDir, "100", 4, Extension));
        }

        [Fact]
        public void SaveMoney_NonInteger_ReturnsFalse()
        {
            WriteSlotFile(1, "CompleteSave", 10, 20);
            Assert.False(CheatGame.SaveMoney(Slot1Path, "12.5", 1, Extension));
        }

        [Fact]
        public void CopySlotToOtherSlot_CopiesSlot1ToSlot2AndRewritesIdentifier()
        {
            WriteSlotFile(1, "CompleteSave", money: 123, experience: 7);
            string dest = Path.Combine(_tempDir, "CompleteSave1." + Extension);
            Assert.False(File.Exists(dest));

            bool ok = CheatGame.CopySlotToOtherSlot(1, 2, _tempDir, "steam");

            Assert.True(ok);
            Assert.True(File.Exists(dest));
            string content = File.ReadAllText(dest);
            Assert.Contains("\"CompleteSave1\"", content);
            Assert.DoesNotContain("\"CompleteSave\":", content);
            Assert.Contains("\"money\":123", content);
        }
    }
}
