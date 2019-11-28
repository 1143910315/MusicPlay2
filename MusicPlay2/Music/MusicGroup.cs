using System;
using System.Collections.Generic;
using System.Text;

namespace MusicPlay2.Music {
    internal class MusicGroup {
        private static MusicGroup instance;
        private MusicGroup() {
        }
        public static MusicGroup GetInstance() {
            if (instance == null) {
                instance = new MusicGroup();
            }
            return instance;
        }
    }
}
