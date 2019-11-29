using System;
using System.Collections.Generic;
using System.Text;

namespace MusicPlay2.Music {
    internal class MusicList {
        private readonly List<uint> idLists = new List<uint>();
        private readonly List<string> fileNameLists = new List<string>();
        public MusicList(uint id, string fileName) {
            AddFile(id, fileName);
        }
        public void AddFile(uint id, string fileName) {
            idLists.Add(id);
            fileNameLists.Add(fileName);
        }
    }
}
